import { h } from 'vue'
import DefaultTheme from 'vitepress/theme'
import TrackSwitch from './TrackSwitch.vue'
import ApiStyle from './ApiStyle.vue'
import './custom.css'

export default {
  extends: DefaultTheme,
  enhanceApp({ app }) {
    app.component('ApiStyle', ApiStyle)
  },
  Layout: () => h(DefaultTheme.Layout, null, {
    'sidebar-nav-before': () => h(TrackSwitch),
  }),
}
