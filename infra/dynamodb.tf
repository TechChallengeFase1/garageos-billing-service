# Banco nao relacional da solucao (ADR 0007): tabela unica do Billing.
#
# Chaves (ver o ADR para o modelo completo):
#   ORC#<id>     / META     orcamento com itens e status
#   ORC#<id>     / PAG#<id> pagamento do orcamento
#   WEBHOOK#<paymentId>#<status>   deduplicacao de notificacoes
#   INBOX#<messageId>              deduplicacao de mensagens (TTL)
#   OUTBOX#<id>                    eventos a publicar
resource "aws_dynamodb_table" "main" {
  name = local.name

  # On-demand: sem capacidade provisionada, sem custo fixo. Nada e cobrado
  # enquanto o ambiente esta parado.
  billing_mode = "PAY_PER_REQUEST"

  hash_key  = "PK"
  range_key = "SK"

  attribute {
    name = "PK"
    type = "S"
  }

  attribute {
    name = "SK"
    type = "S"
  }

  attribute {
    name = "osId"
    type = "S"
  }

  attribute {
    name = "mpPaymentId"
    type = "S"
  }

  attribute {
    name = "outboxPendente"
    type = "S"
  }

  # Orcamentos e pagamentos de uma OS.
  global_secondary_index {
    name            = "por-os"
    hash_key        = "osId"
    range_key       = "SK"
    projection_type = "ALL"
  }

  # Pagamento a partir do id do Mercado Pago, que e o que chega no webhook.
  global_secondary_index {
    name            = "por-pagamento-mp"
    hash_key        = "mpPaymentId"
    projection_type = "ALL"
  }

  # Indice esparso: so os itens de outbox ainda nao publicados carregam o
  # atributo, entao o publicador consulta sem precisar de Scan.
  global_secondary_index {
    name            = "outbox-pendente"
    hash_key        = "outboxPendente"
    range_key       = "SK"
    projection_type = "ALL"
  }

  # Itens INBOX expiram sozinhos depois do prazo de deduplicacao.
  ttl {
    attribute_name = "expiraEm"
    enabled        = true
  }

  # Ambiente descartavel.
  deletion_protection_enabled = false
}

# Credenciais do Mercado Pago. O Terraform cria o segredo vazio; o valor
# real e gravado a mao, fora do state:
#
#   aws secretsmanager put-secret-value \
#     --secret-id garageos-producao-billing/mercadopago \
#     --secret-string '{"accessToken":"TEST-...","publicKey":"TEST-...","webhookSecret":"..."}'
resource "aws_secretsmanager_secret" "mercadopago" {
  name                    = "${local.name}/mercadopago"
  description             = "Credenciais de teste do Mercado Pago (${local.env})"
  recovery_window_in_days = 0
}
