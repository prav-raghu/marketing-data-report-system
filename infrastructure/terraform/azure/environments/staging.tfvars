location     = "southafricanorth"
environment  = "staging"
project_name = "node-mono-repo-template"

customer_api_image = "acrdev.azurecr.io/customer-api:latest"
admin_api_image    = "acrdev.azurecr.io/admin-api:latest"
customer_web_image = "acrdev.azurecr.io/customer-web:latest"

customer_api_min_replicas = 0
customer_api_max_replicas = 5

admin_api_min_replicas = 0
admin_api_max_replicas = 3

customer_web_min_replicas = 0
customer_web_max_replicas = 5

redis_capacity = 1
redis_family   = "C"
redis_sku      = "Standard"

customer_web_url = "https://staging.example.com"
admin_web_url    = "https://admin.staging.example.com"
admin_web_domain = "admin.staging.example.com"
