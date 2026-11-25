import com.sun.source.tree.*;
import com.sun.source.util.*;
import java.io.*;
import java.nio.file.*;
import java.util.*;
import javax.tools.*;

class ReadSpringMappings {
    static final Map<String,String> VERBS = Map.of("GetMapping","GET","PostMapping","POST","PutMapping","PUT",
        "PatchMapping","PATCH","DeleteMapping","DELETE");
    record Mapping(List<String> paths, List<String> verbs, boolean qualified, long line) {}

    static String simple(String name) {
        return name.substring(name.lastIndexOf('.') + 1);
    }

    static List<String> literals(ExpressionTree expr) {
        if (expr instanceof LiteralTree value && value.getValue() instanceof String literal)
            return List.of(literal);
        if (expr instanceof NewArrayTree array && array.getInitializers() != null) {
            List<String> result = new ArrayList<>();
            for (ExpressionTree value : array.getInitializers()) {
                var matches = literals(value);
                if (matches.isEmpty()) return List.of();
                result.addAll(matches);
            }
            return result;
        }
        return List.of();
    }

    static List<String> verbs(ExpressionTree expr) {
        if (expr instanceof MemberSelectTree member && simple(member.getExpression().toString()).equals("RequestMethod"))
            return List.of(member.getIdentifier().toString());
        if (expr instanceof NewArrayTree array && array.getInitializers() != null) {
            List<String> result = new ArrayList<>();
            for (ExpressionTree value : array.getInitializers()) {
                var matches = verbs(value);
                if (matches.isEmpty()) return List.of();
                result.addAll(matches);
            }
            return result;
        }
        return List.of();
    }

    static boolean springAnnotation(AnnotationTree annotation, CompilationUnitTree unit) {
        String name = annotation.getAnnotationType().toString();
        if (name.startsWith("org.springframework.web.bind.annotation.")) return true;
        return unit.getImports().stream().anyMatch(item -> {
            String imported = item.getQualifiedIdentifier().toString();
            return !item.isStatic() && (imported.equals("org.springframework.web.bind.annotation." + name)
                || imported.equals("org.springframework.web.bind.annotation.*"));
        });
    }

    static Mapping mapping(ModifiersTree modifiers, CompilationUnitTree unit, SourcePositions positions) {
        var annotations = modifiers.getAnnotations().stream()
            .filter(a -> VERBS.containsKey(simple(a.getAnnotationType().toString()))
                || simple(a.getAnnotationType().toString()).equals("RequestMapping")).toList();
        if (annotations.isEmpty()) return new Mapping(List.of(""), List.of(), false, 0);
        if (annotations.size() != 1 || !springAnnotation(annotations.getFirst(), unit)) return null;
        var annotation = annotations.getFirst();
        var type = simple(annotation.getAnnotationType().toString());
        List<String> paths = List.of(""), methods = VERBS.containsKey(type) ? List.of(VERBS.get(type)) : List.of();
        boolean qualified = false, pathSeen = false;
        for (ExpressionTree argument : annotation.getArguments()) {
            String key = argument instanceof AssignmentTree assignment ? assignment.getVariable().toString() : "value";
            ExpressionTree expr = argument instanceof AssignmentTree assignment ? assignment.getExpression() : argument;
            if (key.equals("value") || key.equals("path")) {
                if (pathSeen) return null;
                pathSeen = true;
                paths = literals(expr);
                if (paths.isEmpty() || paths.stream().anyMatch(path -> path.contains("${") || path.contains("#{"))) return null;
            } else if (key.equals("method")) {
                methods = verbs(expr);
                if (methods.isEmpty()) return null;
            } else if (Set.of("headers", "params", "consumes", "produces", "version").contains(key)) {
                qualified = true;
            }
        }
        return new Mapping(paths, methods, qualified, unit.getLineMap().getLineNumber(positions.getStartPosition(unit, annotation)));
    }

    static boolean conditional(ModifiersTree modifiers) {
        return modifiers.getAnnotations().stream().map(a -> simple(a.getAnnotationType().toString()))
            .anyMatch(name -> name.equals("Profile") || name.startsWith("Conditional"));
    }

    public static void main(String[] args) throws Exception {
        Path root = Path.of(args[0]).toAbsolutePath();
        JavaCompiler compiler = ToolProvider.getSystemJavaCompiler();
        var diagnostics = new DiagnosticCollector<JavaFileObject>();
        try (var manager = compiler.getStandardFileManager(diagnostics, Locale.ROOT, java.nio.charset.StandardCharsets.UTF_8);
             var walk = Files.walk(root)) {
            List<Path> files = walk.filter(p -> {
                String relative = "/" + root.relativize(p).toString().replace(File.separatorChar, '/');
                return relative.endsWith(".java") && !relative.contains("/src/test/")
                    && !relative.contains("/src/integrationTest/") && !relative.contains("/target/")
                    && !relative.contains("/build/") && !relative.contains("/generated/");
            }).sorted().toList();
            JavacTask task = (JavacTask) compiler.getTask(null, manager, diagnostics,
                List.of("-proc:none"), null, manager.getJavaFileObjectsFromPaths(files));
            var positions = Trees.instance(task).getSourcePositions();
            for (var unit : task.parse()) {
                String file = root.relativize(Path.of(unit.getSourceFile().toUri())).toString();
                for (var declaration : unit.getTypeDecls()) {
                    if (!(declaration instanceof ClassTree controller) || conditional(controller.getModifiers())
                        || controller.getModifiers().getAnnotations().stream().noneMatch(a ->
                            simple(a.getAnnotationType().toString()).equals("RestController") && springAnnotation(a, unit)))
                        continue;
                    Mapping parent = mapping(controller.getModifiers(), unit, positions);
                    if (parent == null || !parent.verbs().isEmpty() || parent.qualified()) continue;
                    for (var member : controller.getMembers()) {
                        if (!(member instanceof MethodTree method) || method.getBody() == null || conditional(method.getModifiers())) continue;
                        Mapping child = mapping(method.getModifiers(), unit, positions);
                        if (child == null || child.verbs().isEmpty() || child.qualified()) continue;
                        long start = unit.getLineMap().getLineNumber(positions.getStartPosition(unit, method));
                        String owner = (unit.getPackageName() == null ? "" : unit.getPackageName() + ".") + controller.getSimpleName();
                        for (String verb : child.verbs()) for (String prefix : parent.paths()) for (String leaf : child.paths()) {
                            String route = "/" + (prefix + "/" + leaf).replaceAll("/+", "/").replaceAll("^/|/$", "");
                            System.out.printf("%s\t%s\t%s\t%d\t%d\t%d\t%s\t%s%n",
                                verb, route, file, start, parent.line(), child.line(), owner, method.getName());
                        }
                    }
                }
            }
            long errors = diagnostics.getDiagnostics().stream().filter(d -> d.getKind() == Diagnostic.Kind.ERROR).count();
            if (errors != 0) {
                System.err.println("Java parse errors: " + errors);
                System.exit(2);
            }
        }
    }
}
