data "aws_caller_identity" "current" {}

# Publicados pelo garageos-infra-k8s. Se o cluster estiver destruido, o plan
# falha aqui com ParameterNotFound - antes de criar qualquer recurso.
data "aws_ssm_parameter" "oidc_provider_arn" {
  name = "/${var.project}/${local.env}/eks/oidc-provider-arn"
}

data "aws_ssm_parameter" "namespace" {
  name = "/${var.project}/${local.env}/eks/namespace/${local.service}"
}

locals {
  account_id        = data.aws_caller_identity.current.account_id
  oidc_provider_arn = nonsensitive(data.aws_ssm_parameter.oidc_provider_arn.value)
  oidc_issuer       = replace(local.oidc_provider_arn, "/^.*oidc-provider[/]/", "")
  namespace         = nonsensitive(data.aws_ssm_parameter.namespace.value)
}
