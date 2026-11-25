import com.sun.source.tree.*;
import com.sun.source.util.*;
import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.*;
import java.util.*;
import java.util.stream.Collectors;
import javax.lang.model.element.*;
import javax.lang.model.type.*;
import javax.lang.model.util.*;
import javax.tools.*;

public class PaceJavaSymbols {
    private record Dispatch(ExecutableElement declaration, String receiver) { }

    public static void main(String[] args) throws IOException {
        Path directory = Path.of(args[0]);
        List<Path> paths;
        try (var entries = Files.list(directory)) {
            paths = new ArrayList<>();
            for (Path entry : entries.sorted().toList()) {
                try (var files = Files.list(entry)) {
                    paths.addAll(files.toList());
                }
            }
        }

        var compiler = ToolProvider.getSystemJavaCompiler();
        var diagnostics = new DiagnosticCollector<JavaFileObject>();
        try (var manager = compiler.getStandardFileManager(diagnostics, Locale.ROOT, StandardCharsets.UTF_8)) {
            // Analyze the supplied sources only. The working directory is owned by PACE.
            var options = List.of("-proc:none", "-implicit:none", "-Xmaxerrs", "10000", "-classpath", args[1], "-sourcepath", "");
            var sources = manager.getJavaFileObjectsFromPaths(paths);
            var task = (JavacTask) compiler.getTask(null, manager, diagnostics, options, null, sources);
            var units = new ArrayList<CompilationUnitTree>();
            task.parse().forEach(units::add);
            task.analyze();

            var analyzer = new Analyzer(Trees.instance(task), task.getElements(), task.getTypes(), diagnostics);
            for (var unit : units) analyzer.collect(unit);
            for (var unit : units) analyzer.resolve(unit);
            System.out.println("E\t" + diagnostics.getDiagnostics().stream()
                .filter(diagnostic -> diagnostic.getKind() == Diagnostic.Kind.ERROR).count());
        }
    }

    private static final class Analyzer {
        private final Trees trees;
        private final Elements elements;
        private final Types types;
        private final Set<TypeElement> sourceTypes = new LinkedHashSet<>();
        private final Map<TypeElement, Map<String, List<ExecutableElement>>> inherited = new HashMap<>();
        private final Set<String> sourceMethods = new HashSet<>();
        private final Map<TypeElement, Map<String, List<ExecutableElement>>> members = new HashMap<>();
        private final Map<String, List<TypeElement>> subtypes = new HashMap<>();
        private final Map<Dispatch, List<String>> candidates = new HashMap<>();
        private final Map<JavaFileObject, NavigableSet<Long>> errors = new HashMap<>();

        Analyzer(Trees trees, Elements elements, Types types, DiagnosticCollector<JavaFileObject> diagnostics) {
            this.trees = trees;
            this.elements = elements;
            this.types = types;
            for (var diagnostic : diagnostics.getDiagnostics()) {
                if (diagnostic.getKind() == Diagnostic.Kind.ERROR && diagnostic.getSource() != null)
                    errors.computeIfAbsent(diagnostic.getSource(), ignored -> new TreeSet<>()).add(diagnostic.getPosition());
            }
        }

        void collect(CompilationUnitTree unit) {
            new TreePathScanner<Void, Void>() {
                @Override public Void visitClass(ClassTree node, Void unused) {
                    if (trees.getElement(getCurrentPath()) instanceof TypeElement type && !invalid(type.asType()) &&
                        !type.getModifiers().contains(Modifier.ABSTRACT) && type.getKind().isClass())
                        sourceTypes.add(type);
                    return super.visitClass(node, unused);
                }

                @Override public Void visitMethod(MethodTree node, Void unused) {
                    if (node.getReturnType() != null && end(unit, node) >= 0 &&
                        trees.getElement(getCurrentPath()) instanceof ExecutableElement method) {
                        String symbol = symbol(method);
                        if (symbol != null) {
                            sourceMethods.add(symbol);
                            var parentSymbols = overridden(method);
                            System.out.printf(Locale.ROOT, "D\t%s\t%d\t%s%s%n", fileId(unit), start(unit, node), symbol,
                                parentSymbols.isEmpty() ? "" : "\t" + String.join("\t", parentSymbols));
                        }
                    }
                    return super.visitMethod(node, unused);
                }
            }.scan(unit, null);
        }

        void resolve(CompilationUnitTree unit) {
            new TreePathScanner<Void, Void>() {
                @Override public Void visitMethodInvocation(MethodInvocationTree node, Void unused) {
                    long from = start(unit, node);
                    long to = end(unit, node);
                    if (from >= 0 && to > from) {
                        var path = getCurrentPath();
                        var select = new TreePath(path, node.getMethodSelect());
                        var method = trees.getElement(select);
                        String declaration = "";
                        List<String> targets = List.of();
                        if (!hasError(unit, from, to) && method instanceof ExecutableElement executable) {
                            String attributed = symbol(executable);
                            if (attributed != null) {
                                declaration = attributed;
                                targets = targets(node, select, executable, attributed);
                            }
                        }
                        System.out.printf(Locale.ROOT, "C\t%s\t%d\t%d\t%s%s%n", fileId(unit), from, to, declaration,
                            targets.isEmpty() ? "" : "\t" + String.join("\t", targets));
                    }
                    return super.visitMethodInvocation(node, unused);
                }
            }.scan(unit, null);
        }

        private List<String> targets(MethodInvocationTree node, TreePath select, ExecutableElement declaration, String symbol) {
            var flags = declaration.getModifiers();
            var owner = (TypeElement) declaration.getEnclosingElement();
            if (flags.contains(Modifier.STATIC) || flags.contains(Modifier.PRIVATE) ||
                flags.contains(Modifier.FINAL) || owner.getModifiers().contains(Modifier.FINAL))
                return sourceMethods.contains(symbol) ? List.of(symbol) : List.of();

            TypeMirror receiver;
            if (node.getMethodSelect() instanceof MemberSelectTree member) {
                var expression = member.getExpression();
                if (expression.toString().equals("super") || expression.toString().endsWith(".super"))
                    return sourceMethods.contains(symbol) ? List.of(symbol) : List.of();
                receiver = trees.getTypeMirror(new TreePath(select, expression));
            } else {
                receiver = trees.getScope(select).getEnclosingClass().asType();
            }

            if (invalid(receiver)) return List.of();
            TypeMirror bound = types.erasure(receiver);
            return candidates.computeIfAbsent(new Dispatch(declaration, bound.toString()), ignored -> {
                var found = new TreeSet<String>();
                if (!flags.contains(Modifier.ABSTRACT) && sourceMethods.contains(symbol)) found.add(symbol);
                String name = declaration.getSimpleName().toString();
                var receivers = subtypes.computeIfAbsent(bound.toString(), key -> sourceTypes.stream()
                    .filter(type -> types.isSubtype(types.erasure(type.asType()), bound)).toList());
                for (var runtimeType : receivers) {
                    var runtimeMembers = members.computeIfAbsent(runtimeType, type -> ElementFilter.methodsIn(elements.getAllMembers(type))
                        .stream().collect(Collectors.groupingBy(method -> method.getSimpleName().toString())));
                    for (var method : runtimeMembers.getOrDefault(name, List.of())) {
                        if (method.getModifiers().contains(Modifier.ABSTRACT) ||
                            !elements.overrides(method, declaration, runtimeType)) continue;
                        String target = symbol(method);
                        if (sourceMethods.contains(target)) found.add(target);
                    }
                }
                return List.copyOf(found);
            });
        }

        private List<String> overridden(ExecutableElement method) {
            var owner = (TypeElement) method.getEnclosingElement();
            if (method.getModifiers().contains(Modifier.STATIC) || method.getModifiers().contains(Modifier.PRIVATE))
                return List.of();
            return inherited.computeIfAbsent(owner, this::parentMethods)
                .getOrDefault(method.getSimpleName().toString(), List.of()).stream()
                .filter(parent -> elements.overrides(method, parent, owner))
                .map(this::symbol).filter(Objects::nonNull).distinct().sorted().toList();
        }

        private Map<String, List<ExecutableElement>> parentMethods(TypeElement owner) {
            var pending = new ArrayDeque<TypeMirror>(types.directSupertypes(owner.asType()));
            var visited = new HashSet<Element>();
            var byName = new HashMap<String, List<ExecutableElement>>();
            while (!pending.isEmpty()) {
                var type = pending.removeFirst();
                if (invalid(type) || !(types.asElement(type) instanceof TypeElement parent) ||
                    !visited.add(parent) || trees.getPath(parent) == null) continue;
                for (var method : ElementFilter.methodsIn(parent.getEnclosedElements()))
                    byName.computeIfAbsent(method.getSimpleName().toString(), ignored -> new ArrayList<>()).add(method);
                pending.addAll(types.directSupertypes(type));
            }
            return byName;
        }

        private String symbol(ExecutableElement method) {
            if (!(method.getEnclosingElement() instanceof TypeElement owner) || invalid(owner.asType()) ||
                method.getKind() != ElementKind.METHOD) return null;
            var parameters = method.getParameters().stream().map(VariableElement::asType).map(types::erasure).toList();
            if (parameters.stream().anyMatch(Analyzer::invalid)) return null;
            String binaryName = elements.getBinaryName(owner).toString();
            if (binaryName.isEmpty()) return null;
            return binaryName + "#" + method.getSimpleName() + "(" +
                parameters.stream().map(Object::toString).collect(Collectors.joining(",")) + ")";
        }

        private static boolean invalid(TypeMirror type) {
            return type == null || type.getKind() == TypeKind.ERROR || type.getKind() == TypeKind.NONE ||
                type instanceof ArrayType array && invalid(array.getComponentType());
        }

        private boolean hasError(CompilationUnitTree unit, long from, long to) {
            var positions = errors.get(unit.getSourceFile());
            Long first = positions == null ? null : positions.ceiling(from);
            return first != null && first < to;
        }

        private String fileId(CompilationUnitTree unit) {
            return Path.of(unit.getSourceFile().toUri()).getParent().getFileName().toString();
        }

        private long start(CompilationUnitTree unit, Tree node) { return trees.getSourcePositions().getStartPosition(unit, node); }
        private long end(CompilationUnitTree unit, Tree node) { return trees.getSourcePositions().getEndPosition(unit, node); }
    }
}
