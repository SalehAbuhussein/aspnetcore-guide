<script setup lang="ts">
import { computed } from 'vue'
import { useData, useRoute, withBase } from 'vitepress'

const { theme } = useData()
const route = useRoute()

const tracks = [
  { key: 'mvc', label: 'MVC' },
  { key: 'web-api', label: 'Web API' },
]

const current = computed(() => route.path.match(/^\/(mvc|web-api)\//)?.[1])
const page = computed(() => route.path.split('/').pop())

// Same topic in the other track when it exists, else that track's introduction.
function linkFor(key: string) {
  const links = (theme.value.sidebar[`/${key}/`] ?? [])
    .flatMap((g: any) => (g.items ?? [g]).map((i: any) => i.link))
  const same = `/${key}/${page.value}`
  return withBase(links.includes(same) ? same : `/${key}/introduction`)
}
</script>

<template>
  <nav v-if="current" class="track-switch" aria-label="Choose a track">
    <a
      v-for="t in tracks"
      :key="t.key"
      :href="linkFor(t.key)"
      :aria-current="t.key === current ? 'page' : undefined"
    >{{ t.label }}</a>
  </nav>
</template>
