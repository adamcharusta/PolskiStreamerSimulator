import {
  createRootRoute,
  createRoute,
  createRouter,
  type RouterHistory,
} from '@tanstack/react-router';

import { CareerScreen } from '@/features/career/CareerScreen';
import { CreateCareerScreen } from '@/features/create-career/CreateCareerScreen';
import { HistoryScreen } from '@/features/history/HistoryScreen';
import { HomeScreen } from '@/features/home/HomeScreen';
import { RecoverScreen } from '@/features/recovery/RecoverScreen';
import { RecoveryKeyScreen } from '@/features/recovery/RecoveryKeyScreen';

import { RootLayout } from './RootLayout';
import { NotFound, RouteError } from './RouteFallbacks';

const rootRoute = createRootRoute({ component: RootLayout });
const homeRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/',
  component: HomeScreen,
});
const createCareerRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/nowa-kariera',
  component: CreateCareerScreen,
});
const careerRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/kariera/$careerId',
  component: CareerScreen,
});

const historyRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/kariera/$careerId/historia',
  component: HistoryScreen,
});

const keyRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/kariera/$careerId/klucz',
  component: RecoveryKeyScreen,
});

const recoverRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/odzyskaj',
  component: RecoverScreen,
});

const routeTree = rootRoute.addChildren([
  homeRoute,
  createCareerRoute,
  careerRoute,
  historyRoute,
  keyRoute,
  recoverRoute,
]);

/**
 * One game route per career (its stage follows the server's next step, not the URL, TD-98), its history (M3-D), its
 * recovery key and the recovery route (M4-A).
 */
export function createAppRouter(history?: RouterHistory) {
  return createRouter({
    routeTree,
    history,
    defaultNotFoundComponent: NotFound,
    defaultErrorComponent: RouteError,
  });
}

export type AppRouter = ReturnType<typeof createAppRouter>;

declare module '@tanstack/react-router' {
  interface Register {
    router: AppRouter;
  }
}
