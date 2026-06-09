# Reference-repo naming model (briandenicola/online-banking-demo): a random
# pet name + a short random id form the base token applied to every resource,
# so each deployment (and each region/workspace) gets a unique, collision-free
# name like "loyal-pegasus-28475-rg" without any static prefix.
resource "random_pet" "this" {}

resource "random_id" "this" {
  byte_length = 2
}
