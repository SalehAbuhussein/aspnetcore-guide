# Calling from Angular

## Introduction

Most APIs are called from a browser app. This page creates a small Angular app, lists and adds todos from `TodoApi`, and shows validation errors from the API next to the form field.

You need the `/todos` endpoints from [Building CRUD](/web-api/crud), the paged list from [Pagination](/web-api/pagination), and the validation rules from [Validation](/web-api/validation). You also need Node.js 20.19 or later.

## Creating the Angular app

```bash
npx @angular/cli@latest new todo-web --style=css --ssr=false --defaults
cd todo-web
```

The Angular CLI creates the project and installs its packages. `--ssr=false` skips server-side rendering, because the app runs in the browser and the API serves the data. `--defaults` accepts the other prompts. Keep `todo-web` in its own folder next to `TodoApi`, not inside it.

## Proxying API calls in development

```json [proxy.conf.json]
{
  "/todos": {
    "target": "https://localhost:5001",
    "secure": false
  }
}
```

```json [angular.json (excerpt)]
"serve": {
  "builder": "@angular/build:dev-server",
  "options": {
    "proxyConfig": "proxy.conf.json"
  },
```

Replace `5001` with the HTTPS port in your API's `Properties/launchSettings.json`. The Angular dev server runs on `http://localhost:4200`. With this proxy, the browser sends `/todos` to the dev server, and the dev server forwards it to the API. The browser only ever talks to one origin, so you do not need [CORS](/web-api/cors) in development. `secure: false` lets the dev server accept the ASP.NET Core development certificate, which Node does not trust by default.

## Registering HttpClient

```ts [src/app/app.config.ts]
import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideHttpClient, withFetch } from '@angular/common/http';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideHttpClient(withFetch()),
  ],
};
```

`HttpClient` is Angular's service for HTTP calls. `provideHttpClient` makes it available to every component and service. `withFetch` tells it to use the browser's `fetch` API underneath. This app has one page, so the config drops `provideRouter`, and you can delete `src/app/app.routes.ts`.

## Describing the data with TypeScript

```ts [src/app/todo.ts]
export interface Todo {
  id: number;
  title: string;
  isDone: boolean;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface ValidationProblem {
  title: string;
  status?: number;
  errors?: Record<string, string[]>;
}
```

The `Todo` interface matches the JSON the API returns, and `PagedResult` matches the paged list from `GET /todos`. ASP.NET Core writes property names in camelCase, so `IsDone` becomes `isDone`. `ValidationProblem` matches the ProblemDetails body that the API returns for a 400 response. Its `errors` object maps each field name to a list of messages. `status` is optional because the validation response from Minimal APIs can leave it out.

## The TodoService

```ts [src/app/todo.service.ts]
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { map } from 'rxjs';
import { PagedResult, Todo } from './todo';

@Injectable({ providedIn: 'root' })
export class TodoService {
  private readonly http = inject(HttpClient);

  getAll() {
    return this.http
      .get<PagedResult<Todo>>('/todos')
      .pipe(map((result) => result.items));
  }

  create(title: string) {
    return this.http.post<Todo>('/todos', { title });
  }
}
```

A service keeps the HTTP calls out of the component. `providedIn: 'root'` creates one shared instance for the whole app, and `inject` hands it the `HttpClient`. `HttpClient` sends objects as JSON and parses JSON responses, so you set no headers yourself. Each method returns an Observable, a stream that sends the request when something subscribes to it. `map` turns the paged result into the plain list the component needs.

## The TodoList component

```ts [src/app/todo-list.ts]
import { Component, OnInit, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Todo, ValidationProblem } from './todo';
import { TodoService } from './todo.service';

@Component({
  selector: 'app-todo-list',
  template: `
    @if (loading()) {
      <p>Loading...</p>
    } @else if (error()) {
      <p role="alert">{{ error() }}</p>
    } @else {
      <form (submit)="addTodo($event)">
        <label>
          Title
          <input [value]="title()" (input)="title.set($any($event.target).value)" />
        </label>
        @for (msg of fieldErrors()['Title'] ?? []; track msg) {
          <p class="field-error">{{ msg }}</p>
        }
        <button type="submit">Add</button>
      </form>

      <ul>
        @for (todo of todos(); track todo.id) {
          <li>{{ todo.title }} {{ todo.isDone ? '(done)' : '' }}</li>
        }
      </ul>
    }
  `,
})
export class TodoList implements OnInit {
  private readonly todoService = inject(TodoService);

  protected readonly todos = signal<Todo[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly title = signal('');
  protected readonly fieldErrors = signal<Record<string, string[]>>({});

  ngOnInit() {
    this.todoService.getAll().subscribe({
      next: (todos) => {
        this.todos.set(todos);
        this.loading.set(false);
      },
      error: (err: HttpErrorResponse) => {
        this.error.set(`Request failed with status ${err.status}`);
        this.loading.set(false);
      },
    });
  }

  protected addTodo(event: Event) {
    event.preventDefault();
    this.fieldErrors.set({});

    this.todoService.create(this.title()).subscribe({
      next: (created) => {
        this.todos.update((current) => [...current, created]);
        this.title.set('');
      },
      error: (err: HttpErrorResponse) => {
        if (err.status === 400) {
          const problem = err.error as ValidationProblem;
          this.fieldErrors.set(problem.errors ?? {});
          return;
        }
        this.error.set(`Request failed with status ${err.status}`);
      },
    });
  }
}
```

A signal is a value that tells Angular when it changes, so the template updates on its own. `ngOnInit` loads the list once when the component first appears. The `loading` and `error` signals decide what to show while the request runs and when it fails. Unlike `fetch`, `HttpClient` treats 404 and 500 responses as errors and calls the `error` callback.

When the API answers 400, `err.error` holds the parsed ProblemDetails body. The component reads `errors` and shows the messages under the input. The key is `Title` with a capital T, because the API uses the C# property name for error keys. `@if` and `@for` are Angular's built-in control flow blocks, and `track` tells `@for` how to tell items apart.

Show the component from the root component:

```ts [src/app/app.ts]
import { Component } from '@angular/core';
import { TodoList } from './todo-list';

@Component({
  selector: 'app-root',
  imports: [TodoList],
  template: `
    <main>
      <h1>Todos</h1>
      <app-todo-list />
    </main>
  `,
})
export class App {}
```

The root component now has an inline template, so delete `src/app/app.html` and `src/app/app.css`.

## Running both apps

```bash
# terminal 1, in TodoApi
dotnet run --launch-profile https

# terminal 2, in todo-web
npm start
```

`npm start` runs `ng serve`, which reads `proxyConfig` from `angular.json`. You can also pass the file on the command line with `ng serve --proxy-config proxy.conf.json`.

Open `http://localhost:4200`. Add a todo, then submit an empty title to see the validation message under the input. You can also check the proxy from a terminal:

```bash
curl -i http://localhost:4200/todos -H "Content-Type: application/json" -d '{"title":"Buy milk"}'
```

The response is `201 Created` with the new todo, which means the request went through the dev server to the API.

::: tip
If you followed [Authentication](/web-api/authentication), `POST /todos` needs a signed-in user and answers 401 without one. For a quick test, add `-H "Authorization: Bearer <token>"` with a token from `dotnet user-jwts create`. For the browser app, sign in with a cookie as shown in [Cookie auth for SPAs](/web-api/spa-auth).
:::

## In production

`npm run build` type-checks the code and writes static files to `dist/todo-web/browser/`. The dev proxy does not exist in production, so pick one of these:

- Serve the files from the same origin as the API. Copy the contents of `dist/todo-web/browser/` into the API's `wwwroot` folder and add `app.UseStaticFiles()` and `app.MapFallbackToFile("index.html")`, or put both behind one reverse proxy. Relative URLs like `/todos` keep working and you need no CORS.
- Host the Angular app on a different origin, such as a CDN. Then call the API with its full URL and allow that origin with a [CORS](/web-api/cors) policy.

## Learn more

- [CORS](/web-api/cors)
- [Cookie auth for SPAs](/web-api/spa-auth)
- [Angular dev server proxy](https://angular.dev/tools/cli/serve#proxying-to-a-backend-server)
- [Single page apps in ASP.NET Core](https://learn.microsoft.com/aspnet/core/client-side/spa/intro)
