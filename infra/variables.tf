variable "aws_region" {
  description = "Regiao. A mesma do bootstrap - a VPC nao atravessa regioes."
  type        = string
  default     = "us-east-1"
}

variable "project" {
  description = "Prefixo dos recursos"
  type        = string
  default     = "garageos"
}

variable "service_account" {
  description = "Nome da ServiceAccount dos pods, que o manifesto k8s/ do servico cria"
  type        = string
  default     = "billing"
}
