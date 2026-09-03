resource "aws_s3_bucket" "spa" {
  bucket        = "${var.project_name}-${var.environment}-admin-web"
  force_destroy = var.environment != "prod"
}

resource "aws_s3_bucket_public_access_block" "spa" {
  bucket = aws_s3_bucket.spa.id

  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

resource "aws_s3_bucket_versioning" "spa" {
  bucket = aws_s3_bucket.spa.id

  versioning_configuration {
    status = "Enabled"
  }
}

resource "aws_s3_bucket_cors_configuration" "spa" {
  bucket = aws_s3_bucket.spa.id

  cors_rule {
    allowed_headers = ["*"]
    allowed_methods = ["GET", "HEAD"]
    allowed_origins = ["*"]
    max_age_seconds = 3600
  }
}

resource "aws_cloudfront_origin_access_control" "spa" {
  name                              = "${var.project_name}-${var.environment}-admin-web-oac"
  description                       = "OAC for Node Mono Repo Template ${var.environment} admin-web"
  origin_access_control_origin_type = "s3"
  signing_behavior                  = "always"
  signing_protocol                  = "sigv4"
}

resource "aws_cloudfront_distribution" "spa" {
  enabled             = true
  is_ipv6_enabled     = true
  default_root_object = "index.html"
  comment             = "Node Mono Repo Template ${var.environment} admin-web"
  price_class         = "PriceClass_100"

  aliases = var.domain != "" ? [var.domain] : []

  origin {
    domain_name              = aws_s3_bucket.spa.bucket_regional_domain_name
    origin_id                = "S3-${aws_s3_bucket.spa.bucket}"
    origin_access_control_id = aws_cloudfront_origin_access_control.spa.id
  }

  default_cache_behavior {
    allowed_methods        = ["GET", "HEAD", "OPTIONS"]
    cached_methods         = ["GET", "HEAD"]
    target_origin_id       = "S3-${aws_s3_bucket.spa.bucket}"
    viewer_protocol_policy = "redirect-to-https"
    compress               = true

    cache_policy_id          = "658327ea-f89d-4fab-a63d-7e88639e58f6"
    origin_request_policy_id = "88a5eaf4-2fd4-4709-b370-b4c650ea3fcf"
  }

  custom_error_response {
    error_code            = 403
    response_code         = 200
    response_page_path    = "/index.html"
    error_caching_min_ttl = 0
  }

  custom_error_response {
    error_code            = 404
    response_code         = 200
    response_page_path    = "/index.html"
    error_caching_min_ttl = 0
  }

  restrictions {
    geo_restriction {
      restriction_type = "none"
    }
  }

  viewer_certificate {
    cloudfront_default_certificate = var.certificate_arn == ""
    acm_certificate_arn            = var.certificate_arn != "" ? var.certificate_arn : null
    ssl_support_method             = var.certificate_arn != "" ? "sni-only" : null
    minimum_protocol_version       = var.certificate_arn != "" ? "TLSv1.2_2021" : "TLSv1"
  }
}

data "aws_iam_policy_document" "cloudfront_s3" {
  statement {
    actions   = ["s3:GetObject"]
    resources = ["${aws_s3_bucket.spa.arn}/*"]

    principals {
      type        = "Service"
      identifiers = ["cloudfront.amazonaws.com"]
    }

    condition {
      test     = "StringEquals"
      variable = "AWS:SourceArn"
      values   = [aws_cloudfront_distribution.spa.arn]
    }
  }
}

resource "aws_s3_bucket_policy" "spa" {
  bucket = aws_s3_bucket.spa.id
  policy = data.aws_iam_policy_document.cloudfront_s3.json
}
