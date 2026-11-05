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
const confirmation = ref('')
const submitting = ref(false)
const error = ref('')
const secure = isSecureOrigin()
const returnUrl = computed(() => safeReturnUrl(route.query.returnUrl, router))

async function submit() {
  if (submitting.value) return
  error.value = ''
  if (password.value.length < 12 || password.value.length > 128) {
    error.value = 'Hasło musi mieć od 12 do 128 znaków.'
    return
  }
  if (password.value !== confirmation.value) {
    error.value = 'Hasła muszą być identyczne.'
    return
  }
  submitting.value = true
  try {
    await session.register({ email: email.value, password: password.value })
    password.value = ''
    confirmation.value = ''
    await router.replace({ name: 'login', query: { returnUrl: returnUrl.value, registered: '1' } })
  } catch (failure) {
    error.value = authErrorMessage(failure)
    password.value = ''
    confirmation.value = ''
  } finally { submitting.value = false }
}
</script>

<template>
  <section class="account-card" aria-labelledby="register-title">
    <p class="eyebrow">Zacznij od siebie</p>
    <h1 id="register-title">Utwórz konto</h1>
    <p class="intro">Twój prywatny obszar na codzienne finanse.</p>
    <p v-if="!secure" id="https-warning" class="error" role="alert">Do korzystania z konta wymagane jest bezpieczne połączenie HTTPS. Otwórz zaufany adres HTTPS aplikacji.</p>
    <form :aria-busy="submitting" :aria-describedby="!secure ? 'https-warning' : undefined" @submit.prevent="submit">
      <label for="register-email">Email</label>
      <input id="register-email" v-model="email" name="email" type="email" autocomplete="username" required maxlength="256" :disabled="submitting" />
      <label for="register-password">Hasło</label>
      <input id="register-password" v-model="password" name="password" type="password" autocomplete="new-password" required minlength="12" maxlength="128" :disabled="submitting" aria-describedby="password-hint register-error" />
      <p id="password-hint" class="helper">Od 12 do 128 znaków. Możesz użyć długiej frazy.</p>
      <label for="register-confirmation">Powtórz hasło</label>
      <input id="register-confirmation" v-model="confirmation" name="confirmation" type="password" autocomplete="new-password" required minlength="12" maxlength="128" :disabled="submitting" aria-describedby="register-error" />
      <p id="register-error" class="error" role="alert">{{ error }}</p>
      <button type="submit" :disabled="submitting || !secure">{{ submitting ? 'Tworzenie konta…' : 'Utwórz konto' }}</button>
    </form>
    <p class="helper">Po utworzeniu konta zalogujesz się osobno. Potwierdzenie emaila nie jest wymagane.</p>
    <p class="form-footer">Masz już konto? <RouterLink :to="{ name: 'login', query: { returnUrl } }">Zaloguj się</RouterLink></p>
  </section>
</template>
