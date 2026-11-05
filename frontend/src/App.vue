<script setup lang="ts">
import { RouterLink, RouterView } from 'vue-router'
</script>

<template>
  <a class="skip-link" href="#main">Przejdź do treści</a>
  <header class="site-header">
    <RouterLink class="brand" to="/app" aria-label="PennyPath — strona główna">PennyPath<span aria-hidden="true"> /</span></RouterLink>
    <span class="site-caption">Twoje finanse, własna droga.</span>
  </header>
  <main id="main" tabindex="-1">
    <RouterView />
  </main>
</template>
