region       = "af-south-1"
environment  = "prod"
project_name = "node-mono-repo-template"

customer_api_image = "123456789.dkr.ecr.af-south-1.amazonaws.com/node-mono-repo-template-prod-customer-api:latest"
admin_api_image    = "123456789.dkr.ecr.af-south-1.amazonaws.com/node-mono-repo-template-prod-admin-api:latest"
customer_web_image = "123456789.dkr.ecr.af-south-1.amazonaws.com/node-mono-repo-template-prod-customer-web:latest"

customer_api_cpu          = 512
customer_api_memory       = 1024
customer_api_min_capacity = 2
customer_api_max_capacity = 20

admin_api_cpu          = 256
admin_api_memory       = 512
admin_api_min_capacity = 1
admin_api_max_capacity = 10

customer_web_cpu          = 512
customer_web_memory       = 1024
customer_web_min_capacity = 2
customer_web_max_capacity = 20

redis_node_type       = "cache.r7g.large"
redis_num_cache_nodes = 2

customer_web_url = "https://app.example.com"
admin_web_url    = "https://admin.example.com"
admin_web_domain = "admin.example.com"
certificate_arn  = "arn:aws:acm:us-east-1:YOUR_ACCOUNT_ID:certificate/YOUR_CERT_ID"
