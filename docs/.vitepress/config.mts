import { defineConfig } from 'vitepress'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'

const site = 'https://salehabuhussein.github.io/aspnetcore-guide/'

// First paragraph under "## Introduction", stripped to plain text, as the page description.
function introOf(file: string) {
  const md = readFileSync(file, 'utf8')
  const para = md.split(/^## Introduction\s*$/m)[1]?.trim().split(/\n\s*\n/)[0] ?? ''
  const text = para
    .replace(/`([^`]*)`/g, '$1')
    .replace(/\[([^\]]*)\]\([^)]*\)/g, '$1')
    .replace(/[*_<>]/g, '')
    .replace(/\s+/g, ' ')
    .trim()
  return text.length > 160 ? text.slice(0, 157).replace(/\s+\S*$/, '') + '...' : text
}

const mvc = [
  {
    "text": "Prologue",
    "items": [
      {
        "text": "What's new in .NET 10",
        "link": "/mvc/whats-new"
      },
      {
        "text": "Upgrade guide",
        "link": "/mvc/upgrade"
      },
      {
        "text": "Glossary",
        "link": "/mvc/glossary"
      }
    ]
  },
  {
    "text": "Getting started",
    "items": [
      {
        "text": "Introduction",
        "link": "/mvc/introduction"
      },
      {
        "text": "Installation",
        "link": "/mvc/installation"
      },
      {
        "text": "Project structure",
        "link": "/mvc/project-structure"
      },
      {
        "text": "HTTP basics",
        "link": "/mvc/http-basics"
      },
      {
        "text": "Configuration",
        "link": "/mvc/configuration"
      },
      {
        "text": "Debugging",
        "link": "/mvc/debugging"
      },
      {
        "text": "Your first app",
        "link": "/mvc/first-app"
      }
    ]
  },
  {
    "text": "Architecture concepts",
    "items": [
      {
        "text": "Request lifecycle",
        "link": "/mvc/lifecycle"
      },
      {
        "text": "Dependency injection",
        "link": "/mvc/dependency-injection"
      },
      {
        "text": "Middleware",
        "link": "/mvc/middleware"
      }
    ]
  },
  {
    "text": "The basics",
    "items": [
      {
        "text": "Routing",
        "link": "/mvc/routing"
      },
      {
        "text": "Controllers",
        "link": "/mvc/controllers"
      },
      {
        "text": "Views",
        "link": "/mvc/views"
      },
      {
        "text": "Layouts and partials",
        "link": "/mvc/layouts"
      },
      {
        "text": "Razor in depth",
        "link": "/mvc/razor"
      },
      {
        "text": "Models",
        "link": "/mvc/models"
      },
      {
        "text": "Forms",
        "link": "/mvc/forms"
      },
      {
        "text": "Validation",
        "link": "/mvc/validation"
      },
      {
        "text": "Static files",
        "link": "/mvc/static-files"
      },
      {
        "text": "Sessions and cookies",
        "link": "/mvc/sessions"
      }
    ]
  },
  {
    "text": "Database",
    "items": [
      {
        "text": "Getting started",
        "link": "/mvc/database"
      },
      {
        "text": "Migrations",
        "link": "/mvc/migrations"
      },
      {
        "text": "Building CRUD",
        "link": "/mvc/crud"
      },
      {
        "text": "Relationships",
        "link": "/mvc/relationships"
      },
      {
        "text": "Querying",
        "link": "/mvc/querying"
      },
      {
        "text": "Seeding",
        "link": "/mvc/seeding"
      }
    ]
  },
  {
    "text": "Frontend",
    "items": [
      {
        "text": "View components and tag helpers",
        "link": "/mvc/view-components"
      },
      {
        "text": "CSS and JavaScript",
        "link": "/mvc/assets"
      },
      {
        "text": "Interactive pages with htmx",
        "link": "/mvc/htmx"
      }
    ]
  },
  {
    "text": "Security",
    "items": [
      {
        "text": "Authentication",
        "link": "/mvc/authentication"
      },
      {
        "text": "Authorization",
        "link": "/mvc/authorization"
      }
    ]
  },
  {
    "text": "Features",
    "items": [
      {
        "text": "File uploads",
        "link": "/mvc/file-uploads"
      },
      {
        "text": "Sending email",
        "link": "/mvc/email"
      },
      {
        "text": "Background tasks",
        "link": "/mvc/background-tasks"
      },
      {
        "text": "Rate limiting",
        "link": "/mvc/rate-limiting"
      },
      {
        "text": "Logging",
        "link": "/mvc/logging"
      },
      {
        "text": "Error handling",
        "link": "/mvc/errors"
      },
      {
        "text": "Localization",
        "link": "/mvc/localization"
      }
    ]
  },
  {
    "text": "Testing",
    "items": [
      {
        "text": "Testing",
        "link": "/mvc/testing"
      },
      {
        "text": "Browser tests",
        "link": "/mvc/browser-tests"
      }
    ]
  },
  {
    "text": "Shipping",
    "items": [
      {
        "text": "Deployment",
        "link": "/mvc/deployment"
      }
    ]
  },
  {
    "text": "How-to guides",
    "items": [
      {
        "text": "Send a welcome email",
        "link": "/mvc/howto-welcome-email"
      },
      {
        "text": "Add login",
        "link": "/mvc/howto-login"
      },
      {
        "text": "Sign in with Google",
        "link": "/mvc/howto-external-login"
      },
      {
        "text": "Reset a forgotten password",
        "link": "/mvc/howto-password-reset"
      },
      {
        "text": "Add an admin role",
        "link": "/mvc/howto-admin-role"
      },
      {
        "text": "Export data to CSV",
        "link": "/mvc/howto-csv-export"
      },
      {
        "text": "Run a job every night",
        "link": "/mvc/howto-nightly-job"
      },
      {
        "text": "Switch to PostgreSQL",
        "link": "/mvc/howto-postgres"
      },
      {
        "text": "Deploy to Azure",
        "link": "/mvc/howto-deploy-azure"
      },
      {
        "text": "Deploy with Docker",
        "link": "/mvc/howto-deploy-docker"
      }
    ]
  },
  {
    "text": "Advanced",
    "link": "/mvc/advanced"
  },
  {
    "text": "Architecture",
    "items": [
      {
        "text": "Organizing a larger app",
        "link": "/mvc/organizing"
      }
    ]
  },
  {
    "text": "Performance",
    "items": [
      {
        "text": "Caching",
        "link": "/mvc/caching"
      },
      {
        "text": "Async and cancellation",
        "link": "/mvc/async"
      },
      {
        "text": "EF Core performance",
        "link": "/mvc/ef-performance"
      }
    ]
  },
  {
    "text": "Background work",
    "items": [
      {
        "text": "Queues and retries",
        "link": "/mvc/queues"
      }
    ]
  },
  {
    "text": "Integrations",
    "items": [
      {
        "text": "Calling other APIs",
        "link": "/mvc/http-clients"
      },
      {
        "text": "Real-time with SignalR",
        "link": "/mvc/real-time"
      }
    ]
  },
  {
    "text": "Data",
    "items": [
      {
        "text": "Transactions and concurrency",
        "link": "/mvc/transactions"
      }
    ]
  },
  {
    "text": "Production",
    "items": [
      {
        "text": "Health checks",
        "link": "/mvc/health-checks"
      },
      {
        "text": "Observability",
        "link": "/mvc/observability"
      },
      {
        "text": "Secrets and hardening",
        "link": "/mvc/security-hardening"
      },
      {
        "text": "CI/CD",
        "link": "/mvc/ci-cd"
      },
      {
        "text": "Running locally with Aspire",
        "link": "/mvc/aspire"
      }
    ]
  },
  {
    "text": "Testing deeper",
    "items": [
      {
        "text": "Unit tests and mocking",
        "link": "/mvc/unit-testing"
      },
      {
        "text": "Testing with real databases",
        "link": "/mvc/testcontainers"
      }
    ]
  },
  {
    "text": "Help",
    "items": [
      {
        "text": "CLI cheat sheet",
        "link": "/mvc/cli"
      },
      {
        "text": "Troubleshooting",
        "link": "/mvc/troubleshooting"
      }
    ]
  }
]

const api = [
  {
    "text": "Prologue",
    "items": [
      {
        "text": "What's new in .NET 10",
        "link": "/web-api/whats-new"
      },
      {
        "text": "Upgrade guide",
        "link": "/web-api/upgrade"
      },
      {
        "text": "Glossary",
        "link": "/web-api/glossary"
      }
    ]
  },
  {
    "text": "Getting started",
    "items": [
      {
        "text": "Introduction",
        "link": "/web-api/introduction"
      },
      {
        "text": "Installation",
        "link": "/web-api/installation"
      },
      {
        "text": "Project structure",
        "link": "/web-api/project-structure"
      },
      {
        "text": "HTTP basics",
        "link": "/web-api/http-basics"
      },
      {
        "text": "Configuration",
        "link": "/web-api/configuration"
      },
      {
        "text": "Debugging",
        "link": "/web-api/debugging"
      },
      {
        "text": "Your first API",
        "link": "/web-api/first-app"
      }
    ]
  },
  {
    "text": "Architecture concepts",
    "items": [
      {
        "text": "Request lifecycle",
        "link": "/web-api/lifecycle"
      },
      {
        "text": "Dependency injection",
        "link": "/web-api/dependency-injection"
      },
      {
        "text": "Middleware",
        "link": "/web-api/middleware"
      }
    ]
  },
  {
    "text": "The basics",
    "items": [
      {
        "text": "Minimal APIs",
        "link": "/web-api/minimal-apis"
      },
      {
        "text": "Controllers",
        "link": "/web-api/controllers"
      },
      {
        "text": "Routing",
        "link": "/web-api/routing"
      },
      {
        "text": "Requests",
        "link": "/web-api/requests"
      },
      {
        "text": "Responses",
        "link": "/web-api/responses"
      },
      {
        "text": "Validation",
        "link": "/web-api/validation"
      },
      {
        "text": "Pagination",
        "link": "/web-api/pagination"
      },
      {
        "text": "OpenAPI",
        "link": "/web-api/openapi"
      }
    ]
  },
  {
    "text": "Database",
    "items": [
      {
        "text": "Getting started",
        "link": "/web-api/database"
      },
      {
        "text": "Migrations",
        "link": "/web-api/migrations"
      },
      {
        "text": "Building CRUD",
        "link": "/web-api/crud"
      },
      {
        "text": "Relationships",
        "link": "/web-api/relationships"
      },
      {
        "text": "Querying",
        "link": "/web-api/querying"
      },
      {
        "text": "Seeding",
        "link": "/web-api/seeding"
      }
    ]
  },
  {
    "text": "Frontend",
    "items": [
      {
        "text": "Calling from Angular",
        "link": "/web-api/angular-client"
      },
      {
        "text": "Cookie auth for SPAs",
        "link": "/web-api/spa-auth"
      },
      {
        "text": "Refresh tokens",
        "link": "/web-api/refresh-tokens"
      }
    ]
  },
  {
    "text": "Security",
    "items": [
      {
        "text": "Authentication",
        "link": "/web-api/authentication"
      },
      {
        "text": "Authorization",
        "link": "/web-api/authorization"
      },
      {
        "text": "CORS",
        "link": "/web-api/cors"
      }
    ]
  },
  {
    "text": "Features",
    "items": [
      {
        "text": "File uploads",
        "link": "/web-api/file-uploads"
      },
      {
        "text": "Sending email",
        "link": "/web-api/email"
      },
      {
        "text": "Background tasks",
        "link": "/web-api/background-tasks"
      },
      {
        "text": "Rate limiting",
        "link": "/web-api/rate-limiting"
      },
      {
        "text": "Logging",
        "link": "/web-api/logging"
      },
      {
        "text": "Error handling",
        "link": "/web-api/errors"
      },
      {
        "text": "Localization",
        "link": "/web-api/localization"
      }
    ]
  },
  {
    "text": "Testing",
    "items": [
      {
        "text": "Integration tests",
        "link": "/web-api/testing"
      }
    ]
  },
  {
    "text": "Shipping",
    "items": [
      {
        "text": "Deployment",
        "link": "/web-api/deployment"
      }
    ]
  },
  {
    "text": "How-to guides",
    "items": [
      {
        "text": "Send a welcome email",
        "link": "/web-api/howto-welcome-email"
      },
      {
        "text": "Add login",
        "link": "/web-api/howto-login"
      },
      {
        "text": "Protect an API with keys",
        "link": "/web-api/howto-api-keys"
      },
      {
        "text": "Reset a forgotten password",
        "link": "/web-api/howto-password-reset"
      },
      {
        "text": "Add an admin role",
        "link": "/web-api/howto-admin-role"
      },
      {
        "text": "Export data to CSV",
        "link": "/web-api/howto-csv-export"
      },
      {
        "text": "Run a job every night",
        "link": "/web-api/howto-nightly-job"
      },
      {
        "text": "Switch to PostgreSQL",
        "link": "/web-api/howto-postgres"
      },
      {
        "text": "Deploy to Azure",
        "link": "/web-api/howto-deploy-azure"
      },
      {
        "text": "Deploy with Docker",
        "link": "/web-api/howto-deploy-docker"
      }
    ]
  },
  {
    "text": "Advanced",
    "link": "/web-api/advanced"
  },
  {
    "text": "Architecture",
    "items": [
      {
        "text": "Organizing a larger app",
        "link": "/web-api/organizing"
      }
    ]
  },
  {
    "text": "Performance",
    "items": [
      {
        "text": "Caching",
        "link": "/web-api/caching"
      },
      {
        "text": "Async and cancellation",
        "link": "/web-api/async"
      },
      {
        "text": "EF Core performance",
        "link": "/web-api/ef-performance"
      }
    ]
  },
  {
    "text": "Background work",
    "items": [
      {
        "text": "Queues and retries",
        "link": "/web-api/queues"
      }
    ]
  },
  {
    "text": "Integrations",
    "items": [
      {
        "text": "Calling other APIs",
        "link": "/web-api/http-clients"
      },
      {
        "text": "Real-time with SignalR",
        "link": "/web-api/real-time"
      }
    ]
  },
  {
    "text": "Data",
    "items": [
      {
        "text": "Transactions and concurrency",
        "link": "/web-api/transactions"
      }
    ]
  },
  {
    "text": "API extras",
    "items": [
      {
        "text": "Versioning",
        "link": "/web-api/versioning"
      }
    ]
  },
  {
    "text": "Production",
    "items": [
      {
        "text": "Health checks",
        "link": "/web-api/health-checks"
      },
      {
        "text": "Observability",
        "link": "/web-api/observability"
      },
      {
        "text": "Secrets and hardening",
        "link": "/web-api/security-hardening"
      },
      {
        "text": "CI/CD",
        "link": "/web-api/ci-cd"
      },
      {
        "text": "Running locally with Aspire",
        "link": "/web-api/aspire"
      }
    ]
  },
  {
    "text": "Testing deeper",
    "items": [
      {
        "text": "Unit tests and mocking",
        "link": "/web-api/unit-testing"
      },
      {
        "text": "Testing with real databases",
        "link": "/web-api/testcontainers"
      }
    ]
  },
  {
    "text": "Help",
    "items": [
      {
        "text": "CLI cheat sheet",
        "link": "/web-api/cli"
      },
      {
        "text": "Troubleshooting",
        "link": "/web-api/troubleshooting"
      }
    ]
  }
]

// BASE is set by the Pages workflow (/aspnetcore-guide/). Local dev serves from /.
const base = process.env.BASE ?? '/'

export default defineConfig({
  base,
  title: 'ASP.NET Core Guide',
  description: 'A plain, step-by-step guide to ASP.NET Core MVC and Web API on .NET 10.',
  cleanUrls: true,
  lastUpdated: true,
  sitemap: { hostname: site },
  transformPageData(page, { siteConfig }) {
    if (page.frontmatter.description || page.description) return
    const description = introOf(join(siteConfig.srcDir, page.relativePath))
    if (description) page.description = description
  },
  transformHead({ pageData, siteConfig }) {
    const path = pageData.relativePath.replace(/(index)?\.md$/, '')
    const url = site + path
    const name = siteConfig.site.title
    const isHome = path === ''
    const title = isHome || !pageData.title || pageData.title === name ? name : `${pageData.title} | ${name}`
    const description = pageData.frontmatter.description || pageData.description || siteConfig.site.description
    return [
      ['link', { rel: 'canonical', href: url }],
      ['meta', { property: 'og:site_name', content: name }],
      ['meta', { property: 'og:url', content: url }],
      ['meta', { property: 'og:title', content: title }],
      ['meta', { property: 'og:description', content: description }],
      ['meta', { property: 'og:type', content: isHome ? 'website' : 'article' }],
      ['meta', { property: 'og:image:width', content: '2400' }],
      ['meta', { property: 'og:image:height', content: '1260' }],
      ['meta', { property: 'og:image:alt', content: 'ASP.NET Core Guide: Laravel-style docs for .NET 10' }],
    ]
  },
  head: [
    // Apply the saved Minimal APIs / Controllers choice before first paint.
    ['script', {}, "try{if(localStorage.getItem('api-style')==='controllers')document.documentElement.dataset.apiStyle='controllers'}catch(e){}"],
    ['link', { rel: 'icon', href: `${base}logo.svg` }],
    ['meta', { property: 'og:image', content: `https://salehabuhussein.github.io${base}og.png` }],
    ['meta', { name: 'twitter:card', content: 'summary_large_image' }],
    ['link', { rel: 'preconnect', href: 'https://fonts.googleapis.com' }],
    ['link', { rel: 'preconnect', href: 'https://fonts.gstatic.com', crossorigin: '' }],
    ['link', { rel: 'stylesheet', href: 'https://fonts.googleapis.com/css2?family=Figtree:wght@400..800&family=JetBrains+Mono:wght@400;500&display=swap' }],
  ],
  themeConfig: {
    logo: '/logo.svg',
    nav: [
      { text: 'MVC', link: '/mvc/introduction', activeMatch: '^/mvc/' },
      { text: 'Web API', link: '/web-api/introduction', activeMatch: '^/web-api/' },
      { text: '.NET 10', items: [
        { text: 'What\'s new in ASP.NET Core 10', link: 'https://learn.microsoft.com/aspnet/core/release-notes/aspnetcore-10.0' },
        { text: 'Official docs', link: 'https://learn.microsoft.com/aspnet/core/' },
      ]},
    ],
    sidebar: { '/mvc/': mvc, '/web-api/': api },
    outline: { level: [2, 3], label: 'On this page' },
    search: { provider: 'local' },
    docFooter: { prev: 'Previous', next: 'Next' },
    footer: { message: 'Unofficial guide. Not affiliated with Microsoft. Released under the MIT License.' },
    socialLinks: [{ icon: 'github', link: 'https://github.com/SalehAbuhussein/aspnetcore-guide' }],
    editLink: { pattern: 'https://github.com/SalehAbuhussein/aspnetcore-guide/edit/main/docs/:path', text: 'Edit this page on GitHub' },
  },
})
