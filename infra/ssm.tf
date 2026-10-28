# Contrato com a pipeline deste servico, no padrao do ADR 0001: o CD le os
# valores daqui, nunca escritos a mao. So ponteiros - segredo fica no
# Secrets Manager.

locals {
  prefix = "/${var.project}/${local.env}/services/${local.service}"
}

resource "aws_ssm_parameter" "ecr_url" {
  name        = "${local.prefix}/ecr-url"
  description = "URL do repositorio ECR do ${local.service}"
  type        = "String"
  value       = aws_ecr_repository.app.repository_url
}

resource "aws_ssm_parameter" "irsa_role_arn" {
  name        = "${local.prefix}/irsa-role-arn"
  description = "Role da ServiceAccount do ${local.service} (anotacao eks.amazonaws.com/role-arn)"
  type        = "String"
  value       = aws_iam_role.irsa.arn
}

resource "aws_ssm_parameter" "dynamodb_table" {
  name        = "${local.prefix}/dynamodb-table"
  description = "Nome da tabela DynamoDB do ${local.service}"
  type        = "String"
  value       = aws_dynamodb_table.main.name
}

resource "aws_ssm_parameter" "mercadopago_secret_arn" {
  name        = "${local.prefix}/mercadopago-secret-arn"
  description = "ARN do segredo com as credenciais do Mercado Pago"
  type        = "String"
  value       = aws_secretsmanager_secret.mercadopago.arn
}
