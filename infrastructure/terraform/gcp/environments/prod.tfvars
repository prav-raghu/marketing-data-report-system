project_id   = "node-mono-repo-template-prod"
region       = "africa-south1"
environment  = "prod"
project_name = "node-mono-repo-template"

customer_api_image = "africa-south1-docker.pkg.dev/node-mono-repo-template-prod/node-mono-repo-template-prod/customer-api:latest"
admin_api_image    = "africa-south1-docker.pkg.dev/node-mono-repo-template-prod/node-mono-repo-template-prod/admin-api:latest"
customer_web_image = "africa-south1-docker.pkg.dev/node-mono-repo-template-prod/node-mono-repo-template-prod/customer-web:latest"

customer_api_min_instances = 1
customer_api_max_instances = 20
admin_api_min_instances    = 1
admin_api_max_instances    = 10
customer_web_min_instances = 1
customer_web_max_instances = 20

redis_memory_size_gb = 2
redis_tier           = "STANDARD_HA"

customer_web_url = "https://app.example.com"
admin_web_url    = "https://admin.example.com"
admin_web_domain = "admin.example.com"
