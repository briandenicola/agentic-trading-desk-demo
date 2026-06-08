resource "random_pet" "main" {
  length    = 2
  separator = "-"
}

resource "random_id" "main" {
  byte_length = 4
}
