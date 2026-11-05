<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { authErrorMessage } from '@/api/auth'
import { session } from '@/auth/session'

const router = useRouter()
const submitting = ref(false)
const error = ref('')

async function logout() {
  if (submitting.value) return
  submitting.value = true
  error.value = ''
  try {
    await session.logout()
    await router.replace({ name: 'login', query: { returnUrl: router.currentRoute.value.fullPath } })
  } catch (failure) { error.value = authErrorMessage(failure) }
  finally { submitting.value = false }
}
</script>

<template>
  <section v-if="session.state.status === 'authenticated' && session.state.user" class="account-card account-home" aria-labelledby="account-title">
    <p class="eyebrow">Twój prywatny obszar</p>
    <h1 id="account-title">Dobrze Cię widzieć.</h1>
    <p class="intro">Jesteś zalogowany jako <strong class="account-email">{{ session.state.user.email }}</strong></p>
    <div class="empty-state">
      <h2>Tu zacznie się Twoja droga.</h2>
      <p>Miejsce na Twoje wydatki i podsumowania jest już gotowe. Te funkcje pojawią się w kolejnych etapach.</p>
    </div>
    <p class="helper">Sesja trwa maksymalnie 7 dni od logowania. Na współdzielonym urządzeniu wyloguj się po zakończeniu.</p>
    <p class="error" role="alert">{{ error }}</p>
    <button class="secondary" :disabled="submitting" @click="logout">{{ submitting ? 'Wylogowywanie…' : 'Wyloguj się' }}</button>
  </section>
</template>
