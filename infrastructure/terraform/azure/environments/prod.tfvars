location     = "southafricanorth"
environment  = "prod"
project_name = "node-mono-repo-template"

customer_api_image = "acrprod.azurecr.io/customer-api:latest"
admin_api_image    = "acrprod.azurecr.io/admin-api:latest"
customer_web_image = "acrprod.azurecr.io/customer-web:latest"

customer_api_min_replicas = 1
customer_api_max_replicas = 20

admin_api_min_replicas = 1
admin_api_max_replicas = 10

customer_web_min_replicas = 1
customer_web_max_replicas = 20

redis_capacity = 2
redis_family   = "P"
redis_sku      = "Premium"

customer_web_url = "https://app.example.com"
admin_web_url    = "https://admin.example.com"
admin_web_domain = "admin.example.com"
