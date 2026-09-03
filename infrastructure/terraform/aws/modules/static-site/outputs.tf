output "bucket_name" {
  description = "S3 bucket name — upload dist/ build artifacts here"
  value       = aws_s3_bucket.spa.bucket
}

output "cloudfront_domain" {
  description = "CloudFront distribution domain name — use as CNAME target"
  value       = aws_cloudfront_distribution.spa.domain_name
}

output "cloudfront_distribution_id" {
  description = "CloudFront distribution ID — use for cache invalidations after deploys"
  value       = aws_cloudfront_distribution.spa.id
}
