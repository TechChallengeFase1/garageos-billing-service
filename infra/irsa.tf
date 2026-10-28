# IRSA: a identidade AWS dos pods deste servico.
#
# O Kubernetes entrega ao pod um token assinado pelo OIDC do cluster; a AWS
# troca esse token por credenciais desta role, mas so para a ServiceAccount
# deste servico, no namespace dele. Nenhuma chave estatica.
#
# ATENCAO: a trust aponta para o issuer do cluster, que MUDA quando o EKS e
# recriado. Reaplique este Terraform depois de recriar o cluster, senao os
# pods recebem AccessDenied sem nenhuma mudanca de codigo.
data "aws_iam_policy_document" "irsa_trust" {
  statement {
    actions = ["sts:AssumeRoleWithWebIdentity"]

    principals {
      type        = "Federated"
      identifiers = [local.oidc_provider_arn]
    }

    condition {
      test     = "StringEquals"
      variable = "${local.oidc_issuer}:sub"
      values   = ["system:serviceaccount:${local.namespace}:${var.service_account}"]
    }

    condition {
      test     = "StringEquals"
      variable = "${local.oidc_issuer}:aud"
      values   = ["sts.amazonaws.com"]
    }
  }
}

resource "aws_iam_role" "irsa" {
  name               = "${local.name}-irsa"
  description        = "Assumida pelos pods do garageos-billing-service via IRSA"
  assume_role_policy = data.aws_iam_policy_document.irsa_trust.json
}

# Policy INLINE de proposito: a role do CI nao tem iam:CreatePolicy
# (bootstrap/oidc.tf), entao uma aws_iam_policy gerenciada falharia no apply.
data "aws_iam_policy_document" "irsa" {
  # Mensageria (ADR 0006). O MassTransit cria as proprias filas e topicos,
  # sempre com o prefixo deste servico para as filas. Os topicos sao de
  # todos os servicos, porque cada um publica eventos e assina os dos outros.
  statement {
    sid = "FilasDoServico"
    actions = [
      "sqs:CreateQueue",
      "sqs:DeleteQueue",
      "sqs:GetQueueUrl",
      "sqs:GetQueueAttributes",
      "sqs:SetQueueAttributes",
      "sqs:TagQueue",
      "sqs:ListQueueTags",
      "sqs:SendMessage",
      "sqs:ReceiveMessage",
      "sqs:DeleteMessage",
      "sqs:ChangeMessageVisibility",
      "sqs:PurgeQueue",
    ]
    resources = ["arn:aws:sqs:${var.aws_region}:${local.account_id}:${local.name}-*"]
  }

  # Para a saga enviar comandos direto para a fila de outro servico.
  statement {
    sid       = "EnviarParaOutrosServicos"
    actions   = ["sqs:SendMessage", "sqs:GetQueueUrl", "sqs:GetQueueAttributes"]
    resources = ["arn:aws:sqs:${var.aws_region}:${local.account_id}:${var.project}-${local.env}-*"]
  }

  statement {
    sid = "TopicosDaSolucao"
    actions = [
      "sns:CreateTopic",
      "sns:GetTopicAttributes",
      "sns:SetTopicAttributes",
      "sns:Publish",
      "sns:Subscribe",
      "sns:Unsubscribe",
      "sns:ListSubscriptionsByTopic",
      "sns:TagResource",
    ]
    resources = ["arn:aws:sns:${var.aws_region}:${local.account_id}:${var.project}-${local.env}-*"]
  }

  statement {
    sid       = "ListarTopicos"
    actions   = ["sns:ListTopics", "sqs:ListQueues"]
    resources = ["*"]
  }
  # So a tabela deste servico e os indices dela.
  statement {
    sid = "TabelaDoServico"
    actions = [
      "dynamodb:GetItem",
      "dynamodb:PutItem",
      "dynamodb:UpdateItem",
      "dynamodb:DeleteItem",
      "dynamodb:Query",
      "dynamodb:BatchGetItem",
      "dynamodb:BatchWriteItem",
      "dynamodb:ConditionCheckItem",
      "dynamodb:TransactWriteItems",
      "dynamodb:TransactGetItems",
      "dynamodb:DescribeTable",
    ]
    resources = [
      aws_dynamodb_table.main.arn,
      "${aws_dynamodb_table.main.arn}/index/*",
    ]
  }

  # Credenciais do Mercado Pago e URL publica do gateway, usada para
  # montar a notification_url de cada pagamento.
  statement {
    sid       = "SegredoDoServico"
    actions   = ["secretsmanager:GetSecretValue"]
    resources = [aws_secretsmanager_secret.mercadopago.arn]
  }

  statement {
    sid       = "UrlDoGateway"
    actions   = ["ssm:GetParameter"]
    resources = ["arn:aws:ssm:${var.aws_region}:${local.account_id}:parameter/${var.project}/${local.env}/apigw/url"]
  }
}

resource "aws_iam_role_policy" "irsa" {
  name   = "acesso-do-servico"
  role   = aws_iam_role.irsa.id
  policy = data.aws_iam_policy_document.irsa.json
}
