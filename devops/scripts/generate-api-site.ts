import { copyFileSync, existsSync, mkdirSync, readdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);

const API_DIR = resolve(__dirname, "../../documentation/api");
const OUTPUT_DIR = resolve(__dirname, "../../documentation/api-site");

const HTML_TEMPLATE = (title: string, specUrl: string): string => `<!DOCTYPE html>
<html>
  <head>
    <title>${title} - API Documentation</title>
    <meta charset="utf-8"/>
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <link href="https://fonts.googleapis.com/css?family=Montserrat:300,400,700|Roboto:300,400,700" rel="stylesheet">
    <style>
      body { margin: 0; padding: 0; }
    </style>
  </head>
  <body>
    <redoc spec-url='${specUrl}'></redoc>
    <script src="https://cdn.redoc.ly/redoc/latest/bundles/redoc.standalone.js"></script>
  </body>
</html>`;

const INDEX_HTML = (services: string[]): string => `<!DOCTYPE html>
<html>
  <head>
    <title>API Documentation</title>
    <meta charset="utf-8"/>
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <style>
      body {
        font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Oxygen, Ubuntu, Cantarell, sans-serif;
        max-width: 800px;
        margin: 0 auto;
        padding: 40px 20px;
        background: #f5f5f5;
      }
      h1 { color: #333; }
      .services {
        display: grid;
        gap: 20px;
        margin-top: 30px;
      }
      .service {
        background: white;
        padding: 24px;
        border-radius: 8px;
        box-shadow: 0 2px 4px rgba(0,0,0,0.1);
      }
      .service h2 { margin: 0 0 8px 0; color: #2563eb; }
      .service p { margin: 0 0 16px 0; color: #666; }
      .service a {
        display: inline-block;
        background: #2563eb;
        color: white;
        padding: 10px 20px;
        border-radius: 6px;
        text-decoration: none;
        font-weight: 500;
      }
      .service a:hover { background: #1d4ed8; }
    </style>
  </head>
  <body>
    <h1>📚 API Documentation</h1>
    <p>Select a service to view its API documentation:</p>
    <div class="services">
      ${services
          .map(
              (service) => `
        <div class="service">
          <h2>${service}</h2>
          <p>View the OpenAPI documentation for the ${service} service.</p>
          <a href="./${service}/index.html">View Documentation →</a>
        </div>
      `,
          )
          .join("")}
    </div>
  </body>
</html>`;

function generateSite(): void {
    console.info("🌐 Generating API documentation site...\n");

    if (!existsSync(API_DIR)) {
        console.error('❌ API specs not found. Run "pnpm docs:export" first.');
        process.exit(1);
    }

    if (!existsSync(OUTPUT_DIR)) {
        mkdirSync(OUTPUT_DIR, { recursive: true });
    }

    const services = readdirSync(API_DIR).filter((f: string) => {
        const servicePath = resolve(API_DIR, f);
        return existsSync(resolve(servicePath, "openapi.json"));
    });

    if (services.length === 0) {
        console.error('❌ No OpenAPI specs found. Run "pnpm docs:export" first.');
        process.exit(1);
    }

    for (const service of services) {
        console.info(`📦 Processing ${service}...`);

        const serviceOutputDir = resolve(OUTPUT_DIR, service);
        if (!existsSync(serviceOutputDir)) {
            mkdirSync(serviceOutputDir, { recursive: true });
        }

        const specPath = resolve(API_DIR, service, "openapi.json");
        const spec = JSON.parse(readFileSync(specPath, "utf-8"));
        const title = spec.info?.title ?? service;

        copyFileSync(specPath, resolve(serviceOutputDir, "openapi.json"));

        writeFileSync(resolve(serviceOutputDir, "index.html"), HTML_TEMPLATE(title, "./openapi.json"));

        console.info(`   ✅ Generated ${service}/index.html`);
    }

    writeFileSync(resolve(OUTPUT_DIR, "index.html"), INDEX_HTML(services));
    console.info("\n✅ Generated index.html");

    console.info("\n✨ Documentation site generated!");
    console.info(`📁 Output: ${OUTPUT_DIR}`);
    console.info("\nTo serve locally:");
    console.info("  npx serve documentation/api-site");
}

generateSite();
