---
name: mobile
description: Use when working on mobile apps under apps/mobile/. Covers Ionic React + Capacitor — pages, components, hooks, API client, push notifications, native device features (camera, geolocation, storage), offline support, navigation, and Capacitor plugin integration. Also use for new mobile screens, connecting mobile to backend APIs, or native build configuration for iOS/Android.
tools: Read, Edit, Write, Grep, Glob, Bash
model: inherit
---

## App location

```
apps/mobile/
└── customer-mobile/     Ionic React + Capacitor customer app
```

## Tech stack

| Concern | Technology |
|---------|-----------|
| UI Framework | Ionic React (`@ionic/react`) |
| Native Bridge | Capacitor (`@capacitor/core`) |
| Build Tool | Vite |
| Routing | `IonReactRouter` + `IonRouterOutlet` |
| Data Fetching | React Query |
| API Client | Axios (centralized `apiClient.ts`) |
| State | React Context or Zustand |
| Forms | React Hook Form + Zod |
| Styling | Ionic CSS variables + Tailwind utilities |
| Native Plugins | `@capacitor/push-notifications`, `@capacitor/camera`, `@capacitor/geolocation`, `@capacitor/preferences` |

## Directory structure

```
apps/mobile/customer-mobile/src/
├── pages/
├── components/
│   ├── shared/
│   └── product/
├── hooks/
├── services/
│   └── apiClient.ts
├── store/
├── utils/
├── constants/
└── App.tsx
```

Do not deviate from this structure.

## Routing (`App.tsx`)

```typescript
import { IonApp, IonRouterOutlet, setupIonicReact } from '@ionic/react';
import { IonReactRouter } from '@ionic/react-router';
import { Route, Redirect } from 'react-router-dom';

setupIonicReact();

const App: React.FC = () => (
  <IonApp>
    <QueryClientProvider client={queryClient}>
      <IonReactRouter>
        <IonRouterOutlet>
          <Route exact path="/home" component={Home} />
          <Route exact path="/products/:id" component={ProductDetail} />
          <Route exact path="/profile" component={Profile} />
          <Route exact path="/" render={() => <Redirect to="/home" />} />
        </IonRouterOutlet>
      </IonReactRouter>
    </QueryClientProvider>
  </IonApp>
);
```

## API client

Same pattern as the web frontend — centralized Axios with interceptors, but tokens use `@capacitor/preferences`, NOT `localStorage`:

```typescript
import axios from 'axios';
import { Preferences } from '@capacitor/preferences';

const apiClient = axios.create({ baseURL: import.meta.env.VITE_MOBILE_API_BASE_URL, timeout: 15000 });

apiClient.interceptors.request.use(async (config) => {
  const { value: token } = await Preferences.get({ key: 'access_token' });
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

apiClient.interceptors.response.use(
  (response) => response,
  async (error) => {
    if (error.response?.status === 401) await Preferences.remove({ key: 'access_token' });
    return Promise.reject(error);
  }
);

export default apiClient;
```

## Page pattern

Every page follows the Ionic template with all three states (loading, error, empty):

```typescript
import {
  IonPage, IonHeader, IonToolbar, IonTitle, IonContent,
  IonList, IonItem, IonLabel, IonSkeletonText, IonRefresher, IonRefresherContent,
} from '@ionic/react';
import { useProducts } from '../hooks/useProducts';

const Home: React.FC = () => {
  const { data, isLoading, isError, refetch } = useProducts();

  return (
    <IonPage>
      <IonHeader>
        <IonToolbar><IonTitle>Products</IonTitle></IonToolbar>
      </IonHeader>
      <IonContent fullscreen>
        <IonRefresher slot="fixed" onIonRefresh={(e) => { refetch().finally(() => e.detail.complete()); }}>
          <IonRefresherContent />
        </IonRefresher>

        {isLoading && (
          <IonList>
            {Array.from({ length: 6 }).map((_, i) => (
              <IonItem key={i}><IonLabel><IonSkeletonText animated style={{ width: '80%' }} /></IonLabel></IonItem>
            ))}
          </IonList>
        )}

        {isError && (
          <div className="ion-padding ion-text-center">
            <p>Something went wrong.</p>
            <IonButton onClick={() => refetch()}>Retry</IonButton>
          </div>
        )}

        {!isLoading && !isError && data?.data.length === 0 && (
          <div className="ion-padding ion-text-center"><p>No products found.</p></div>
        )}

        {!isLoading && !isError && data?.data.map((product) => <ProductCard key={product.id} product={product} />)}
      </IonContent>
    </IonPage>
  );
};
```

## React Query hooks

`ResponseDto`/`CursorResponseDto` are local types now (`../types/api` or wherever this app keeps its wire-shape types) — there's no shared `@node-mono-repo-template/types` package anymore, since the backend's replacement (`DotNetMonoRepoTemplate.Types`) is a C# library. See `CLAUDE.md`'s "Stack split" section.

```typescript
import { useQuery, useInfiniteQuery } from '@tanstack/react-query';
import apiClient from '../services/apiClient';
import type { ResponseDto, CursorResponseDto } from '../types/api';

export function useProducts() {
  return useInfiniteQuery({
    queryKey: ['products'],
    queryFn: async ({ pageParam }) => {
      const params = pageParam ? { cursor: pageParam } : {};
      const res = await apiClient.get<CursorResponseDto<ProductData>>('/api/products', { params });
      return res.data;
    },
    getNextPageParam: (lastPage) => lastPage.hasMore ? lastPage.nextCursor : undefined,
    initialPageParam: undefined,
    staleTime: 1000 * 60 * 5,
    retry: 2,
  });
}
```

## Infinite scroll (customer-facing lists)

```typescript
import { IonInfiniteScroll, IonInfiniteScrollContent } from '@ionic/react';

const { data, fetchNextPage, hasNextPage } = useProducts();
const allProducts = data?.pages.flatMap((p) => p.data) ?? [];

return (
  <>
    {allProducts.map((product) => <ProductCard key={product.id} product={product} />)}
    <IonInfiniteScroll onIonInfinite={(e) => { fetchNextPage().finally(() => e.target.complete()); }} disabled={!hasNextPage}>
      <IonInfiniteScrollContent loadingText="Loading more..." />
    </IonInfiniteScroll>
  </>
);
```

## Authentication — token storage

Never use `localStorage` on mobile — use `@capacitor/preferences`:

```typescript
import { Preferences } from '@capacitor/preferences';

export async function saveTokens(accessToken: string, refreshToken: string): Promise<void> {
  await Promise.all([
    Preferences.set({ key: 'access_token', value: accessToken }),
    Preferences.set({ key: 'refresh_token', value: refreshToken }),
  ]);
}
```

## Push notifications

```typescript
import { PushNotifications } from '@capacitor/push-notifications';
import { Capacitor } from '@capacitor/core';

async function registerPushNotifications(): Promise<void> {
  if (!Capacitor.isNativePlatform()) return;
  const permission = await PushNotifications.requestPermissions();
  if (permission.receive !== 'granted') return;
  await PushNotifications.register();
  PushNotifications.addListener('registration', async (token) => {
    await apiClient.post('/api/devices/register', { push_token: token.value });
  });
}
```

## Camera

```typescript
import { Camera, CameraResultType, CameraSource } from '@capacitor/camera';

export async function capturePhoto(): Promise<string | undefined> {
  const photo = await Camera.getPhoto({ quality: 80, allowEditing: false, resultType: CameraResultType.Base64, source: CameraSource.Prompt });
  return photo.base64String;
}
```

Upload as multipart to the backend's storage endpoint (`DotNetMonoRepoTemplate.Storage`, wired into whichever service owns the upload) — never send raw base64 to the database.

## Offline support

```typescript
const queryClient = new QueryClient({
  defaultOptions: {
    queries: { networkMode: 'offlineFirst', staleTime: 1000 * 60 * 10, gcTime: 1000 * 60 * 60 * 24 },
  },
});
```

Persist the React Query cache to Preferences for offline reads via `persistQueryClient` + `createSyncStoragePersister`.

## Capacitor config

```typescript
import { CapacitorConfig } from '@capacitor/cli';

const config: CapacitorConfig = {
  appId: 'com.yourcompany.appname',
  appName: 'App Name',
  webDir: 'dist',
  server: { androidScheme: 'https' },
  plugins: { PushNotifications: { presentationOptions: ['badge', 'sound', 'alert'] } },
};

export default config;
```

Update `appId` and `appName` for every new project — never ship with template defaults.

## Native build commands

```bash
pnpm --filter @node-mono-repo-template/customer-mobile build
npx cap sync
npx cap open ios
npx cap open android
npx cap run android
npx cap run ios
```

## Environment variables

```env
VITE_MOBILE_API_BASE_URL=http://localhost:4000
```

For native builds, point to the backend's accessible IP/domain — `localhost` doesn't resolve from a device:
```env
VITE_MOBILE_API_BASE_URL=http://192.168.1.100:4000
```

## Critical rules

Never use `localStorage` — use `@capacitor/preferences` for all persistent storage on device. Never call backend APIs directly from Capacitor native plugins — go through `apiClient`. Never use browser-only APIs without checking `Capacitor.isNativePlatform()`. Always use `IonPage`/`IonHeader`/`IonContent`, never a raw `div` as the page root. Always implement pull-to-refresh (`IonRefresher`) on data-driven pages. Always implement infinite scroll for lists, not pagination buttons. All pages must handle loading skeleton, error state with retry, and empty state. Images from the device camera must go through the API/storage service, never stored locally. `appId` in `capacitor.config.ts` must be updated for every new project before native build.
