# Registry da imagem deste servico. Substitui o Docker Hub da Fase 3: o push
# usa o OIDC que ja existe (sem DOCKERHUB_TOKEN) e os nos ja tem permissao
# de leitura no ECR.
resource "aws_ecr_repository" "app" {
  name                 = local.name
  image_tag_mutability = "IMMUTABLE"

  # Ambiente descartavel: o destroy apaga o repositorio mesmo com imagens.
  force_delete = true

  image_scanning_configuration {
    scan_on_push = true
  }
}

# Guarda so as 10 imagens mais recentes. As tags sao o SHA do commit, entao
# sem isto o repositorio cresce a cada merge.
resource "aws_ecr_lifecycle_policy" "app" {
  repository = aws_ecr_repository.app.name

  policy = jsonencode({
    rules = [{
      rulePriority = 1
      description  = "Manter as 10 imagens mais recentes"
      selection = {
        tagStatus   = "any"
        countType   = "imageCountMoreThan"
        countNumber = 10
      }
      action = { type = "expire" }
    }]
  })
}
