# garageos-billing-service

Microsserviço **Billing** do GarageOS — Tech Challenge Fase 4.

Dono dos orçamentos das ordens de serviço e das cobranças. É o serviço com o banco **não relacional** da solução.

| | |
|---|---|
| Banco | Amazon DynamoDB, tabela única on-demand (ADR 0007) |
| Mensageria | Amazon SQS/SNS com MassTransit 8.x (etapa 5) |
| Pagamento | Mercado Pago (etapa 6) |
| Papel na saga | Participante: gera, cancela e cobra o orçamento |
| Namespace no EKS | `garageos-billing` |

As decisões de arquitetura estão nos ADRs 0004 a 0007 em
[`garageos-os-service/docs/adrs`](https://github.com/TechChallengeFase1/garageos-os-service/tree/main/docs/adrs).

## Rotas

| Método | Rota | Quem | Descrição |
|---|---|---|---|
| `POST` | `/api/orcamentos` | Admin | Gera o orçamento a partir dos itens com preço. Idempotente por OS |
| `GET` | `/api/orcamentos?ordemDeServicoId=` | Admin | Orçamento de uma OS |
| `GET` | `/api/orcamentos/{id}` | Admin ou o cliente dono | Orçamento com itens |
| `POST` | `/api/orcamentos/{id}/enviar` | Admin | Pendente → Enviado |
| `PATCH` | `/api/orcamentos/{id}/resposta` | Admin ou o cliente dono | Aprovação ou recusa |
| `POST` | `/api/orcamentos/{id}/cancelar` | Admin | Cancelamento (compensação). Idempotente |
| `POST` | `/api/orcamentos/{id}/pagamentos` | Admin | Abre a cobrança de um orçamento aprovado. Idempotente |
| `GET` | `/api/orcamentos/{id}/pagamentos[/{pagamentoId}]` | Admin | Cobranças do orçamento |
| `GET` | `/health/live` · `/health/ready` | pública | Health checks (o ready faz `DescribeTable`) |

"Cliente dono" é o token do login por CPF cujo `sub` é o `clienteId` do orçamento.

### Modelo da tabela

| PK | SK | Item |
|---|---|---|
| `ORC#{id}` | `META` | Orçamento, com os itens embutidos |
| `ORC#{id}` | `PAG#{id}` | Pagamento do orçamento |
| `OS#{osId}` | `ORCAMENTO` | Marcador: garante um orçamento por OS |

Orçamento e marcador são gravados na mesma transação com `attribute_not_exists`, o que torna a geração idempotente mesmo com requisições simultâneas. Alterações usam concorrência otimista pelo atributo `version`.

## Como rodar localmente

```bash
cp .env.example .env   # preencha o JWT
docker compose up -d --build
```

Sobe a API e o DynamoDB Local; a tabela é criada na subida. Swagger em <http://localhost:8081/swagger>.

## Testes

```bash
dotnet test Code/GarageOS.Billing.UnitTests           # sem dependências
dotnet test Code/GarageOS.Billing.IntegrationTests    # precisa de Docker (DynamoDB Local via Testcontainers)
```

## Infraestrutura (`infra/`)

Terraform com o que é só deste serviço: repositório ECR, banco, role IRSA dos pods e
parâmetros SSM em `/garageos/producao/services/billing/*`. O state fica em
`services/billing/terraform.tfstate`, no bucket compartilhado.

Pré-requisitos, nesta ordem:

1. Este repositório listado em `github_repos` no bootstrap (`garageos-infra-database/bootstrap`), com o apply feito.
2. Cluster no ar pelo `garageos-infra-k8s`, que publica o OIDC do cluster e o namespace deste serviço.
3. Depois do primeiro apply, gravar as credenciais de teste do Mercado Pago no segredo `garageos-producao-billing/mercadopago`.

O workflow [`infra.yml`](.github/workflows/infra.yml) faz plan no PR e apply no merge na `main`.
Depois de recriar o cluster, reaplique: a trust da role IRSA aponta para o OIDC do cluster, que muda.
