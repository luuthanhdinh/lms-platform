# Frontend — React App

Read this before: creating any component, adding a route, fetching data,
or touching authentication.

---

## Stack

| Concern | Library |
|---|---|
| Framework | React 19 + TypeScript |
| Build | Vite |
| Routing | TanStack Router (file-based) |
| Data fetching | TanStack Query v5 |
| Global state | Zustand |
| Auth | keycloak-js (in-memory only — never localStorage) |
| Forms | React Hook Form + Zod |
| HTTP | Axios via central `apiClient` |
| UI components | shadcn/ui |
| Styling | Tailwind CSS |
| Icons | lucide-react |

---

## Folder structure

```
frontend/
├── src/
│   ├── app/
│   │   ├── router.tsx           # TanStack Router root
│   │   ├── providers.tsx        # QueryClientProvider, KeycloakProvider
│   │   └── main.tsx
│   ├── features/                # feature-sliced architecture
│   │   ├── auth/
│   │   │   ├── components/      # LoginPage, ProtectedRoute
│   │   │   └── hooks/           # useAuth, useCurrentUser
│   │   ├── courses/
│   │   │   ├── api/             # query/mutation hooks
│   │   │   ├── components/      # CourseCard, CourseSyllabus, etc.
│   │   │   └── pages/           # CataloguePage, CourseDetailPage
│   │   ├── enrollment/
│   │   ├── content/             # VideoPlayer, PdfViewer, ScormPlayer
│   │   ├── progress/
│   │   ├── assessment/
│   │   └── certificate/
│   ├── components/
│   │   └── ui/                  # shadcn/ui base components (Button, Card, etc.)
│   ├── lib/
│   │   ├── api-client.ts        # Axios instance — single entry point for all API calls
│   │   ├── keycloak.ts          # Keycloak-js singleton
│   │   └── query-client.ts      # TanStack Query client config
│   └── types/                   # shared TypeScript interfaces (mirror OpenAPI schemas)
├── index.html
├── vite.config.ts
├── tailwind.config.ts
└── package.json
```

---

## Authentication — Keycloak-js

### Rule: JWT lives in memory only — never `localStorage`, never `sessionStorage`

```typescript
// src/lib/keycloak.ts
import Keycloak from 'keycloak-js';

const keycloak = new Keycloak({
  url:      import.meta.env.VITE_KEYCLOAK_URL,
  realm:    import.meta.env.VITE_KEYCLOAK_REALM,
  clientId: import.meta.env.VITE_KEYCLOAK_CLIENT_ID,
});

export default keycloak;
```

```typescript
// src/app/main.tsx
import keycloak from '@/lib/keycloak';

keycloak.init({
  onLoad: 'check-sso',          // silently check SSO on load; don't force login
  silentCheckSsoRedirectUri:    // iframe-based silent SSO check
    window.location.origin + '/silent-check-sso.html',
  pkceMethod: 'S256',           // PKCE for public clients
}).then(authenticated => {
  if (authenticated) {
    // Start token refresh timer (refresh 60s before expiry)
    keycloak.onTokenExpired = () => keycloak.updateToken(60);
  }
  ReactDOM.createRoot(document.getElementById('root')!).render(
    <App authenticated={authenticated} />
  );
});
```

### Auth hook

```typescript
// src/features/auth/hooks/useAuth.ts
import keycloak from '@/lib/keycloak';

export function useAuth() {
  return {
    isAuthenticated: keycloak.authenticated ?? false,
    token:           keycloak.token,
    userId:          keycloak.tokenParsed?.sub as string,
    tenantId:        keycloak.tokenParsed?.tenant_id as string,
    roles:           keycloak.tokenParsed?.realm_access?.roles as string[] ?? [],
    hasRole:         (role: string) => keycloak.hasRealmRole(role),
    login:           () => keycloak.login(),
    logout:          () => keycloak.logout({ redirectUri: window.location.origin }),
  };
}
```

### Protected route wrapper

```typescript
// src/features/auth/components/ProtectedRoute.tsx
import { useAuth } from '../hooks/useAuth';

interface Props {
  children: React.ReactNode;
  roles?: string[];              // if provided, user must have at least one
}

export function ProtectedRoute({ children, roles }: Props) {
  const { isAuthenticated, hasRole, login } = useAuth();

  if (!isAuthenticated) {
    login();
    return null;
  }

  if (roles && !roles.some(hasRole)) {
    return <div>Access denied.</div>;
  }

  return <>{children}</>;
}
```

---

## API Client

### Rule: all API calls go through this client — never create Axios instances elsewhere

```typescript
// src/lib/api-client.ts
import axios from 'axios';
import keycloak from './keycloak';

export const apiClient = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL,  // points to YARP gateway
  headers: { 'Content-Type': 'application/json' },
});

// Attach JWT on every request (from keycloak-js in-memory token)
apiClient.interceptors.request.use(async config => {
  if (keycloak.token) {
    // Refresh token if it expires in < 30 seconds
    await keycloak.updateToken(30).catch(() => keycloak.login());
    config.headers.Authorization = `Bearer ${keycloak.token}`;
  }
  return config;
});

// Redirect to login on 401
apiClient.interceptors.response.use(
  res => res,
  err => {
    if (err.response?.status === 401) keycloak.login();
    return Promise.reject(err);
  }
);
```

---

## TanStack Query patterns

### Data fetching hook (one file per API resource)

```typescript
// src/features/courses/api/use-courses.ts
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '@/lib/api-client';
import type { Course, CourseSummary, PagedResult } from '@/types';

// ── Query keys ────────────────────────────────────────────────────────────
export const courseKeys = {
  all:    () => ['courses']                           as const,
  list:   (params: object) => ['courses', params]     as const,
  detail: (id: string) => ['courses', id]             as const,
};

// ── Queries ───────────────────────────────────────────────────────────────
export function useCourses(params: {
  page?: number; pageSize?: number; category?: string;
}) {
  return useQuery({
    queryKey: courseKeys.list(params),
    queryFn: () => apiClient
      .get<PagedResult<CourseSummary>>('/api/courses', { params })
      .then(r => r.data),
    staleTime: 5 * 60 * 1000,   // catalogue data: 5-min cache
  });
}

export function useCourse(courseId: string) {
  return useQuery({
    queryKey: courseKeys.detail(courseId),
    queryFn: () => apiClient
      .get<Course>(`/api/courses/${courseId}`)
      .then(r => r.data),
    enabled: !!courseId,
  });
}

// ── Mutations ─────────────────────────────────────────────────────────────
export function useCreateCourse() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (data: CreateCourseRequest) =>
      apiClient.post<Course>('/api/courses', data).then(r => r.data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: courseKeys.all() });
    },
  });
}

export function usePublishCourse() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (courseId: string) =>
      apiClient.post<Course>(`/api/courses/${courseId}/publish`).then(r => r.data),
    onSuccess: (_, courseId) => {
      qc.invalidateQueries({ queryKey: courseKeys.detail(courseId) });
      qc.invalidateQueries({ queryKey: courseKeys.all() });
    },
  });
}
```

### Using a query in a component

```typescript
// src/features/courses/pages/CataloguePage.tsx
import { useCourses } from '../api/use-courses';
import { CourseCard } from '../components/CourseCard';

export function CataloguePage() {
  const { data, isLoading, isError } = useCourses({ page: 1, pageSize: 24 });

  if (isLoading) return <CourseSkeleton />;
  if (isError)   return <ErrorBanner />;

  return (
    <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
      {data?.data.map(course => (
        <CourseCard key={course.id} course={course} />
      ))}
    </div>
  );
}
```

---

## TanStack Router — route structure

```typescript
// src/app/router.tsx
import { createRouter, createRoute, createRootRoute } from '@tanstack/react-router';
import { ProtectedRoute } from '@/features/auth/components/ProtectedRoute';

const rootRoute = createRootRoute({ component: RootLayout });

// Public routes
const catalogueRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/courses',
  component: CataloguePage,
});

const courseDetailRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/courses/$courseId',
  component: CourseDetailPage,
});

// Protected routes
const dashboardRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/dashboard',
  component: () => (
    <ProtectedRoute>
      <DashboardPage />
    </ProtectedRoute>
  ),
});

const lessonRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/courses/$courseId/lessons/$lessonId',
  component: () => (
    <ProtectedRoute>
      <LessonPage />
    </ProtectedRoute>
  ),
});

// Instructor-only
const courseEditorRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/instructor/courses/$courseId/edit',
  component: () => (
    <ProtectedRoute roles={['instructor', 'admin']}>
      <CourseEditorPage />
    </ProtectedRoute>
  ),
});

export const router = createRouter({
  routeTree: rootRoute.addChildren([
    catalogueRoute,
    courseDetailRoute,
    dashboardRoute,
    lessonRoute,
    courseEditorRoute,
  ]),
});
```

---

## Global State — Zustand

Only use Zustand for truly global UI state (not server state — that's TanStack Query).

```typescript
// src/features/content/store/player-store.ts
import { create } from 'zustand';

interface PlayerState {
  currentLessonId: string | null;
  isPlaying: boolean;
  volume: number;
  setLesson: (id: string) => void;
  setPlaying: (v: boolean) => void;
  setVolume: (v: number) => void;
}

export const usePlayerStore = create<PlayerState>(set => ({
  currentLessonId: null,
  isPlaying: false,
  volume: 1,
  setLesson:   id => set({ currentLessonId: id }),
  setPlaying:  v  => set({ isPlaying: v }),
  setVolume:   v  => set({ volume: v }),
}));
```

---

## Form pattern — React Hook Form + Zod

```typescript
// src/features/courses/components/CreateCourseForm.tsx
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useCreateCourse } from '../api/use-courses';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';

const schema = z.object({
  title:      z.string().min(3).max(200),
  category:   z.string().min(1),
  difficulty: z.enum(['beginner', 'intermediate', 'advanced']),
  language:   z.string().default('vi'),
});
type FormData = z.infer<typeof schema>;

export function CreateCourseForm() {
  const { register, handleSubmit, formState: { errors } } = useForm<FormData>({
    resolver: zodResolver(schema),
  });
  const createCourse = useCreateCourse();

  return (
    <form onSubmit={handleSubmit(data => createCourse.mutate(data))}>
      <Input {...register('title')} placeholder="Course title" />
      {errors.title && <p className="text-red-500">{errors.title.message}</p>}
      {/* ... other fields */}
      <Button type="submit" disabled={createCourse.isPending}>
        {createCourse.isPending ? 'Creating...' : 'Create Course'}
      </Button>
    </form>
  );
}
```

---

## TypeScript types — mirror OpenAPI schemas

```typescript
// src/types/index.ts — keep in sync with OpenAPI specs in docs/

export interface PaginatedMeta {
  page: number; pageSize: number; totalCount: number; totalPages: number;
}

export interface PagedResult<T> {
  data: T[];
  meta: PaginatedMeta;
}

export interface CourseSummary {
  id: string;
  title: string;
  description: string;
  thumbnailUrl: string | null;
  category: string;
  tags: string[];
  difficulty: 'beginner' | 'intermediate' | 'advanced';
  language: string;
  instructorName: string;
  enrollmentCount: number;
  lessonCount: number;
  totalDurationSeconds: number;
  status: 'draft' | 'published';
  publishedAt: string | null;
}

export interface Course extends CourseSummary {
  tenantId: string;
  instructorId: string;
  version: number;
  sections: SectionWithLessons[];
  prerequisites: { courseId: string; courseTitle: string }[];
}

export interface SectionWithLessons {
  id: string;
  title: string;
  order: number;
  lessons: LessonSummary[];
}

export interface LessonSummary {
  id: string;
  title: string;
  durationSeconds: number | null;
  isFreePreview: boolean;
  isOptional: boolean;
  contentType: 'video' | 'pdf' | 'scorm' | 'h5p';
  order: number;
}

export interface Enrollment {
  id: string;
  userId: string;
  courseId: string;
  status: 'pending' | 'active' | 'completed' | 'suspended';
  enrolledAt: string;
  completedAt: string | null;
  expiresAt: string | null;
}

export interface CourseProgress {
  courseId: string;
  completionPercent: number;
  lessonsCompleted: number;
  totalRequiredLessons: number;
  lastAccessedAt: string | null;
  completedAt: string | null;
}

// Request types
export interface CreateCourseRequest {
  title: string;
  description: string;
  category: string;
  tags: string[];
  difficulty: 'beginner' | 'intermediate' | 'advanced';
  language: string;
}
```

---

## Video Player — playback heartbeat

```typescript
// src/features/content/components/VideoPlayer.tsx
import { useEffect, useRef } from 'react';
import { apiClient } from '@/lib/api-client';

interface Props {
  contentItemId: string;
  lessonId: string;
  streamUrl: string;
  resumePositionSeconds: number;
}

export function VideoPlayer({ contentItemId, lessonId, streamUrl, resumePositionSeconds }: Props) {
  const videoRef = useRef<HTMLVideoElement>(null);
  const heartbeatRef = useRef<ReturnType<typeof setInterval>>();

  useEffect(() => {
    const video = videoRef.current;
    if (!video) return;
    video.currentTime = resumePositionSeconds;

    // Heartbeat every 30s — reports position to ContentService
    heartbeatRef.current = setInterval(() => {
      apiClient.post(`/api/content/${contentItemId}/progress`, {
        positionSeconds: Math.floor(video.currentTime),
        totalSeconds:    Math.floor(video.duration),
        lessonId,
      }).catch(() => {});  // silent fail — offline or network error
    }, 30_000);

    return () => clearInterval(heartbeatRef.current);
  }, [contentItemId, lessonId, resumePositionSeconds]);

  return (
    <video
      ref={videoRef}
      src={streamUrl}
      controls
      className="w-full rounded-lg"
    />
  );
}
```

---

## SCORM Player — sandboxed iframe + postMessage bridge (ADR-020)

```typescript
// src/features/content/components/ScormPlayer.tsx
import { useEffect, useRef } from 'react';
import { apiClient } from '@/lib/api-client';

interface Props { contentItemId: string; lessonId: string; courseId: string; }

export function ScormPlayer({ contentItemId, lessonId, courseId }: Props) {
  const iframeRef = useRef<HTMLIFrameElement>(null);

  useEffect(() => {
    const handler = (event: MessageEvent) => {
      if (event.origin !== window.location.origin) return;
      const { type, data } = event.data ?? {};

      if (type === 'SCORM_SET_VALUE' && data.element === 'cmi.core.lesson_status') {
        if (data.value === 'passed' || data.value === 'completed') {
          // Mark lesson complete in ProgressService
          apiClient.post(`/api/progress/lessons/${lessonId}/complete`, {
            courseId,
            watchPercent: 100,
          });
        }
      }
    };

    window.addEventListener('message', handler);
    return () => window.removeEventListener('message', handler);
  }, [lessonId, courseId]);

  return (
    <iframe
      ref={iframeRef}
      src={`/scorm-player/${contentItemId}`}
      sandbox="allow-scripts allow-forms allow-same-origin"
      referrerPolicy="no-referrer"
      className="w-full h-[600px] rounded-lg border"
      title="SCORM content"
    />
  );
}
```

---

## Environment variables — `frontend/.env.local`

```bash
VITE_API_BASE_URL=http://localhost:5000         # YARP gateway
VITE_KEYCLOAK_URL=http://localhost:8080
VITE_KEYCLOAK_REALM=lms
VITE_KEYCLOAK_CLIENT_ID=lms-spa
```

## `vite.config.ts`

```typescript
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';
import path from 'path';

export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: { '@': path.resolve(__dirname, './src') },
  },
  server: {
    port: 5173,
    // Proxy only needed if NOT using Aspire to wire the gateway URL
    // proxy: { '/api': 'http://localhost:5000', '/verify': 'http://localhost:5000' },
  },
});
```

---

## Keycloak client registration (in lms-realm.json)

```json
{
  "clientId": "lms-spa",
  "publicClient": true,
  "standardFlowEnabled": true,
  "implicitFlowEnabled": false,
  "directAccessGrantsEnabled": false,
  "redirectUris": ["http://localhost:5173/*", "https://your-production-domain/*"],
  "webOrigins": ["http://localhost:5173", "https://your-production-domain"],
  "protocol": "openid-connect",
  "attributes": {
    "pkce.code.challenge.method": "S256"
  }
}
```

---

## Key frontend → backend mapping

| Page / feature | API calls |
|---|---|
| Course catalogue | `GET /api/courses` |
| Course detail | `GET /api/courses/{id}` · `GET /api/courses/{id}/syllabus` |
| Enroll | `POST /api/enrollments` |
| My courses (dashboard) | `GET /api/enrollments/me` · `GET /api/progress/me` |
| Lesson player | `GET /api/content/{id}/stream` → video URL · `POST /api/content/{id}/progress` every 30s |
| Mark complete | `POST /api/progress/lessons/{id}/complete` |
| Take quiz | `POST /api/assessments/{id}/sessions` → `POST /api/assessments/sessions/{sid}/submit` |
| My certificates | `GET /api/certificates/me` |
| Verify certificate | `GET /verify/{code}` (public, no auth) |
| Instructor: edit course | `PUT /api/courses/{id}` · section/lesson CRUD |
| Instructor: upload content | `POST /api/content/upload` → S3 direct → `POST /api/content/{id}/process` |
