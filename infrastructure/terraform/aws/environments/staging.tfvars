region       = "af-south-1"
environment  = "staging"
project_name = "node-mono-repo-template"

customer_api_image = "123456789.dkr.ecr.af-south-1.amazonaws.com/node-mono-repo-template-staging-customer-api:latest"
admin_api_image    = "123456789.dkr.ecr.af-south-1.amazonaws.com/node-mono-repo-template-staging-admin-api:latest"
customer_web_image = "123456789.dkr.ecr.af-south-1.amazonaws.com/node-mono-repo-template-staging-customer-web:latest"

customer_api_cpu          = 256
customer_api_memory       = 512
customer_api_min_capacity = 1
customer_api_max_capacity = 5

admin_api_cpu          = 256
admin_api_memory       = 512
admin_api_min_capacity = 1
admin_api_max_capacity = 3

customer_web_cpu          = 256
customer_web_memory       = 512
customer_web_min_capacity = 1
customer_web_max_capacity = 5

redis_node_type       = "cache.t3.micro"
redis_num_cache_nodes = 1

customer_web_url = "https://staging.example.com"
admin_web_url    = "https://admin.staging.example.com"
admin_web_domain = "admin.staging.example.com"
certificate_arn  = ""
