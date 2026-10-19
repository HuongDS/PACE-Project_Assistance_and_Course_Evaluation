using PACE.Application.Analysis.Models;
using PACE.Application.Analysis.Template;
using PACE.Infrastructure.Analysis;
using PACE.Infrastructure.Analysis.Parsing;
using Xunit;

namespace PACE.Tests;

public class HonoParserTests
{
    private static List<TemplateEndpoint> Endpoints(string source)
    {
        var parsed = new JsTsTreeSitterParser().Parse([new SourceFile("/source/catalog.routes.ts", "catalog.routes.ts", source)], default);
        Assert.Empty(parsed.Warnings);
        return BusinessTemplateGenerator.ExtractEndpoints(parsed.Methods);
    }
    [Fact] public void GenericHonoFactoryAndFluentHandlersBecomeEndpoints()
    {
        var endpoints = Endpoints("""
            import { Hono, type Context } from 'hono';
            export function createCatalogRoute(service: CatalogService) {
              return new Hono<AppEnv>()
                .get('/topics', async (c) => c.json({items: await service.listTopics()}))
                .post('/topics', requireAuth, zValidator('json', schema), async (c) => {
                  const session = c.get('session');
                  return c.json(await service.create(c.req.valid('json')));
                });
            }
            """);
        Assert.Equal(2, endpoints.Count);
        Assert.Contains(endpoints, e => e.HttpMethod == "GET" && e.Path == "/topics");
        Assert.Contains(endpoints, e => e.HttpMethod == "POST" && e.Path == "/topics");
        Assert.DoesNotContain(endpoints, e => e.Path == "session");
    }
    [Fact] public void AliasedHonoAndLocalVariablesAreSupported()
    {
        var endpoints = Endpoints("""
            import { Hono as Router } from 'hono';
            export function createRoutes() {
              const api = new Router();
              const configured = api.basePath('/api');
              configured.get('/health', c => c.json({ok:true}));
              api.post('/users', c => c.json({ok:true})).delete('/users/:id', c => c.json({ok:true}));
              return api;
            }
            """);
        Assert.Equal(3, endpoints.Count);
        Assert.Contains(endpoints, e => e.HttpMethod == "DELETE" && e.Path == "/users/:id");
    }
    [Fact] public void ContextGetsAndUnrelatedFluentLibrariesAreNotInventedAsRoutes()
    {
        Assert.Empty(Endpoints("""
            import { Hono } from 'hono';
            const store = new Map();
            store.get('/not-a-route', () => 'not an endpoint');
            new Something().get('/not-a-route', () => 'not an endpoint');
            """));
    }
    [Fact] public void ScannerDoesNotDiscardHonoWhenFrontendDependenciesCoexist()
    {
        var root = Path.Combine(Path.GetTempPath(), "pace-hono-test-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "package.json"), """{"dependencies":{"hono":"4","react":"19"}}""");
            File.WriteAllText(Path.Combine(root, "routes.ts"), "import { Hono } from 'hono'; export const endpoint = new Hono().get('/health', c => c.json({ok: true}));");
            var scan = new RepositoryScanner().Scan(root, default);
            Assert.Single(scan.JsTsFiles);
            var parsed = new JsTsTreeSitterParser().Parse(scan.JsTsFiles, default);
            Assert.Single(BusinessTemplateGenerator.ExtractEndpoints(parsed.Methods));
        }
        finally { Directory.Delete(root, true); }
    }
}
