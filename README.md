# garageos-billing-service

Microsservico **Billing Service** do GarageOS — Tech Challenge Fase 4.

Geracao e envio do orcamento, aprovacao ou recusa pelo cliente, pagamento via Mercado Pago e webhook.

| | |
|---|---|
| Banco | Amazon DynamoDB (on-demand) |
| Mensageria | Amazon SQS/SNS com MassTransit 8.x |
| Papel na saga | Participante — ver ADR 0005 no `garageos-app` |
| Namespace no EKS | `garageos-billing` |

> Repositório em construção. As decisões de arquitetura estão nos ADRs 0004 a 0007 em
> [`garageos-app/docs/adrs`](https://github.com/TechChallengeFase1/garageos-app/tree/main/docs/adrs).

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
