<script setup lang="ts">
import { computed, ref } from 'vue'
import { RouterLink, useRoute, useRouter } from 'vue-router'
import { authErrorMessage, isSecureOrigin } from '@/api/auth'
import { session } from '@/auth/session'
import { safeReturnUrl } from '@/router'

const route = useRoute()
const router = useRouter()
const email = ref('')
const password = ref('')
const submitting = ref(false)
const error = ref('')
const secure = isSecureOrigin()
const returnUrl = computed(() => safeReturnUrl(route.query.returnUrl, router))

async function submit() {
  if (submitting.value) return
  submitting.value = true
  error.value = ''
  try {
    await session.login({ email: email.value, password: password.value })
    password.value = ''
    await router.replace(returnUrl.value)
  } catch (failure) {
    error.value = authErrorMessage(failure)
    password.value = ''
  } finally { submitting.value = false }
}

async function retrySession() {
  if (submitting.value) return
  submitting.value = true
  error.value = ''
  try {
    if (await session.ensureUser()) await router.replace(returnUrl.value)
  } catch (failure) { error.value = authErrorMessage(failure) }
  finally { submitting.value = false }
}
</script>

<template>
  <section class="account-card" aria-labelledby="login-title">
    <p class="eyebrow">Witaj z powrotem</p>
    <h1 id="login-title">Zaloguj się</h1>
    <p class="intro">Wejdź do swojego prywatnego obszaru PennyPath.</p>
    <p v-if="route.query.registered === '1'" class="notice" role="status">Konto zostało utworzone. Zaloguj się, aby kontynuować.</p>
    <p v-if="!secure" id="https-warning" class="error" role="alert">Do korzystania z konta wymagane jest bezpieczne połączenie HTTPS. Otwórz zaufany adres HTTPS aplikacji.</p>
    <div v-if="session.state.status === 'error'" class="notice" role="alert">
      <p>Nie udało się sprawdzić sesji. Spróbuj ponownie.</p>
      <button class="secondary" :disabled="submitting" @click="retrySession">Sprawdź sesję ponownie</button>
    </div>
    <form :aria-busy="submitting" :aria-describedby="!secure ? 'https-warning' : undefined" @submit.prevent="submit">
      <label for="login-email">Email</label>
      <input id="login-email" v-model="email" name="email" type="email" autocomplete="username" required maxlength="256" :disabled="submitting" />
      <label for="login-password">Hasło</label>
      <input id="login-password" v-model="password" name="password" type="password" autocomplete="current-password" required maxlength="128" :disabled="submitting" aria-describedby="login-error" />
      <p id="login-error" class="error" role="alert">{{ error }}</p>
      <button type="submit" :disabled="submitting || !secure">{{ submitting ? 'Logowanie…' : 'Zaloguj się' }}</button>
    </form>
    <p class="helper">Sesja jest zapamiętywana na tym urządzeniu maksymalnie przez 7 dni od logowania.</p>
    <p class="form-footer">Nie masz konta? <RouterLink :to="{ name: 'register', query: { returnUrl } }">Utwórz konto</RouterLink></p>
  </section>
</template>
