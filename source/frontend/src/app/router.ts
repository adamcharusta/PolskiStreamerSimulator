import {
  createRootRoute,
  createRoute,
  createRouter,
  type RouterHistory,
} from '@tanstack/react-router';

import { CareerScreen } from '@/features/career/CareerScreen';
import { CreateCareerScreen } from '@/features/create-career/CreateCareerScreen';
import { HomeScreen } from '@/features/home/HomeScreen';

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

const routeTree = rootRoute.addChildren([homeRoute, createCareerRoute, careerRoute]);

/** One game route per career: its stage follows the server's next step, not the URL (TD-98). */
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
