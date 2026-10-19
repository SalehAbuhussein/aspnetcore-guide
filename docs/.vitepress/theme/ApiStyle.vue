<script lang="ts">
import { ref } from 'vue'

// Shared across every <ApiStyle /> on the page, so all switches stay in sync.
const style = ref<'minimal' | 'controllers'>('minimal')
let loaded = false
</script>

<script setup lang="ts">
import { onMounted } from 'vue'

const options = [
  { key: 'minimal', label: 'Minimal APIs' },
  { key: 'controllers', label: 'Controllers' },
] as const

onMounted(() => {
  if (loaded) return
  loaded = true
  try { style.value = localStorage.getItem('api-style') === 'controllers' ? 'controllers' : 'minimal' } catch {}
  document.documentElement.dataset.apiStyle = style.value
})

function pick(key: 'minimal' | 'controllers') {
  style.value = key
  document.documentElement.dataset.apiStyle = key
  try { localStorage.setItem('api-style', key) } catch {}
}
</script>

<template>
  <div class="api-style" role="radiogroup" aria-label="Code style">
    <span class="api-style-label">Show code as</span>
    <button
      v-for="o in options"
      :key="o.key"
      type="button"
      role="radio"
      :aria-checked="style === o.key"
      @click="pick(o.key)"
    >{{ o.label }}</button>
  </div>
</template>
