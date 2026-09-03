terraform {
  required_version = ">= 1.7.0"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
  }

  backend "s3" {
    bucket         = "node-mono-repo-template-tfstate"
    key            = "terraform/state/aws.tfstate"
    region         = "af-south-1"
    encrypt        = true
    dynamodb_table = "node-mono-repo-template-tfstate-lock"
  }
}

provider "aws" {
  region = var.region

  default_tags {
    tags = {
      Project     = var.project_name
      Environment = var.environment
      ManagedBy   = "terraform"
    }
  }
}
