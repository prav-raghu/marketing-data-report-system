import { existsSync, mkdirSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);

interface OpenAPISpec {
    openapi: string;
    info: {
        title: string;
        version: string;
        description?: string;
    };
    paths: Record<string, unknown>;
    components?: Record<string, unknown>;
}

interface PostmanCollection {
    info: {
        name: string;
        schema: string;
        description?: string;
    };
    item: PostmanItem[];
    variable: PostmanVariable[];
}

interface PostmanItem {
    name: string;
    request: {
        method: string;
        header: PostmanHeader[];
        url: {
            raw: string;
            host: string[];
            path: string[];
            query?: PostmanQuery[];
        };
        body?: {
            mode: string;
            raw: string;
            options?: { raw: { language: string } };
        };
    };
}

interface PostmanHeader {
    key: string;
    value: string;
    type: string;
}

interface PostmanQuery {
    key: string;
    value: string;
}

interface PostmanVariable {
    key: string;
    value: string;
}

interface BrunoRequest {
    name: string;
    method: string;
    url: string;
    headers?: Record<string, string>;
    body?: { type: string; content: string };
}

const SERVICES = [
    { name: "customer-api", port: 3001, baseUrl: "http://localhost:3001" },
    { name: "admin-api", port: 3002, baseUrl: "http://localhost:3002" },
    { name: "schedule-api", port: 3003, baseUrl: "http://localhost:3003" },
    { name: "api-gateway", port: 4000, baseUrl: "http://localhost:4000" },
];

const OUTPUT_DIR = resolve(__dirname, "../../documentation/api");

async function fetchOpenAPISpec(baseUrl: string): Promise<OpenAPISpec | null> {
    try {
        const response = await fetch(`${baseUrl}/docs/json`);
        if (!response.ok) {
            return null;
        }
        return (await response.json()) as OpenAPISpec;
    } catch {
        return null;
    }
}

function convertToPostmanCollection(spec: OpenAPISpec, serviceName: string, baseUrl: string): PostmanCollection {
    const collection: PostmanCollection = {
        info: {
            name: `${spec.info.title} - ${serviceName}`,
            schema: "https://schema.getpostman.com/json/collection/v2.1.0/collection.json",
            description: spec.info.description,
        },
        item: [],
        variable: [
            { key: "baseUrl", value: baseUrl },
            { key: "accessToken", value: "" },
        ],
    };

    for (const [path, methods] of Object.entries(spec.paths)) {
        const methodsRecord = methods as Record<
            string,
            {
                summary?: string;
                requestBody?: {
                    content?: { "application/json"?: { schema?: unknown } };
                };
            }
        >;
        for (const [method, details] of Object.entries(methodsRecord)) {
            const item: PostmanItem = {
                name: details.summary ?? `${method.toUpperCase()} ${path}`,
                request: {
                    method: method.toUpperCase(),
                    header: [
                        { key: "Content-Type", value: "application/json", type: "text" },
                        {
                            key: "Authorization",
                            value: "Bearer {{accessToken}}",
                            type: "text",
                        },
                    ],
                    url: {
                        raw: `{{baseUrl}}${path}`,
                        host: ["{{baseUrl}}"],
                        path: path.split("/").filter(Boolean),
                    },
                },
            };

            if (details.requestBody?.content?.["application/json"]?.schema) {
                item.request.body = {
                    mode: "raw",
                    raw: JSON.stringify({}, null, 2),
                    options: { raw: { language: "json" } },
                };
            }

            collection.item.push(item);
        }
    }

    return collection;
}

function generateBrunoCollection(spec: OpenAPISpec, _serviceName: string, baseUrl: string): BrunoRequest[] {
    const requests: BrunoRequest[] = [];

    for (const [path, methods] of Object.entries(spec.paths)) {
        const methodsRecord = methods as Record<string, { summary?: string }>;
        for (const [method, details] of Object.entries(methodsRecord)) {
            requests.push({
                name: details.summary ?? `${method.toUpperCase()} ${path}`,
                method: method.toUpperCase(),
                url: `${baseUrl}${path}`,
                headers: {
                    "Content-Type": "application/json",
                    Authorization: "Bearer {{accessToken}}",
                },
            });
        }
    }

    return requests;
}

function generateBrunoFile(request: BrunoRequest): string {
    return `meta {
  name: ${request.name}
  type: http
  seq: 1
}

${request.method.toLowerCase()} {
  url: ${request.url}
  body: json
  auth: bearer
}

headers {
  Content-Type: application/json
}

auth:bearer {
  token: {{accessToken}}
}
`;
}

async function exportDocs(): Promise<void> {
    if (!existsSync(OUTPUT_DIR)) {
        mkdirSync(OUTPUT_DIR, { recursive: true });
    }

    console.info("📚 Exporting API Documentation...\n");

    for (const service of SERVICES) {
        console.info(`\n📦 Processing ${service.name}...`);

        const spec = await fetchOpenAPISpec(service.baseUrl);

        if (!spec) {
            console.warn(`   ⚠️  Could not fetch OpenAPI spec (is ${service.name} running on port ${service.port}?)`);
            continue;
        }

        const serviceDir = resolve(OUTPUT_DIR, service.name);
        if (!existsSync(serviceDir)) {
            mkdirSync(serviceDir, { recursive: true });
        }

        writeFileSync(resolve(serviceDir, "openapi.json"), JSON.stringify(spec, null, 2));
        console.info(`   ✅ OpenAPI spec exported`);

        const postmanCollection = convertToPostmanCollection(spec, service.name, service.baseUrl);
        writeFileSync(resolve(serviceDir, `${service.name}.postman_collection.json`), JSON.stringify(postmanCollection, null, 2));
        console.info(`   ✅ Postman collection exported`);

        const brunoDir = resolve(serviceDir, "bruno");
        if (!existsSync(brunoDir)) {
            mkdirSync(brunoDir, { recursive: true });
        }

        const brunoRequests = generateBrunoCollection(spec, service.name, service.baseUrl);
        for (const request of brunoRequests) {
            const fileName = request.name.replaceAll(/[^a-zA-Z0-9]/g, "-").toLowerCase();
            writeFileSync(resolve(brunoDir, `${fileName}.bru`), generateBrunoFile(request));
        }
        console.info(`   ✅ Bruno collection exported (${brunoRequests.length} requests)`);
    }

    console.info("\n✨ API documentation export complete!");
    console.info(`📁 Output directory: ${OUTPUT_DIR}`);
}

(async (): Promise<void> => {
    await exportDocs();
})();
