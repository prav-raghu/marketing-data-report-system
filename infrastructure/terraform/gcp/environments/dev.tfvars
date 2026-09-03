project_id   = "node-mono-repo-template-dev"
region       = "africa-south1"
environment  = "dev"
project_name = "node-mono-repo-template"

customer_api_image = "africa-south1-docker.pkg.dev/node-mono-repo-template-dev/node-mono-repo-template-dev/customer-api:latest"
admin_api_image    = "africa-south1-docker.pkg.dev/node-mono-repo-template-dev/node-mono-repo-template-dev/admin-api:latest"
customer_web_image = "africa-south1-docker.pkg.dev/node-mono-repo-template-dev/node-mono-repo-template-dev/customer-web:latest"

customer_api_min_instances = 0
customer_api_max_instances = 3
admin_api_min_instances    = 0
admin_api_max_instances    = 2
customer_web_min_instances = 0
customer_web_max_instances = 3

redis_memory_size_gb = 1
redis_tier           = "BASIC"

customer_web_url = ""
admin_web_url    = ""
admin_web_domain = ""
