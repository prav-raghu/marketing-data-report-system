project_id   = "node-mono-repo-template-staging"
region       = "africa-south1"
environment  = "staging"
project_name = "node-mono-repo-template"

customer_api_image = "africa-south1-docker.pkg.dev/node-mono-repo-template-staging/node-mono-repo-template-staging/customer-api:latest"
admin_api_image    = "africa-south1-docker.pkg.dev/node-mono-repo-template-staging/node-mono-repo-template-staging/admin-api:latest"
customer_web_image = "africa-south1-docker.pkg.dev/node-mono-repo-template-staging/node-mono-repo-template-staging/customer-web:latest"

customer_api_min_instances = 0
customer_api_max_instances = 5
admin_api_min_instances    = 0
admin_api_max_instances    = 3
customer_web_min_instances = 0
customer_web_max_instances = 5

redis_memory_size_gb = 1
redis_tier           = "BASIC"

customer_web_url = "https://staging.example.com"
admin_web_url    = "https://admin.staging.example.com"
admin_web_domain = "admin.staging.example.com"
