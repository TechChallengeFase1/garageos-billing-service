output "ecr_url" {
  value = aws_ecr_repository.app.repository_url
}

output "irsa_role_arn" {
  value = aws_iam_role.irsa.arn
}

output "dynamodb_table" {
  value = aws_dynamodb_table.main.name
}
