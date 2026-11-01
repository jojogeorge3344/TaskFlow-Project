import { Routes } from '@angular/router';

/** The routes as the router sees them: each empty-path `loadChildren` entry replaced by the routes it loads. */
export async function expandRoutes(routes: Routes): Promise<Routes> {
  const expanded: Routes = [];
  for (const route of routes) {
    if (route.path === '' && route.loadChildren) {
      const children = (await (route.loadChildren as () => Promise<Routes>)()) as Routes;
      expanded.push(...(await expandRoutes(children)));
    } else {
      expanded.push(route);
    }
  }
  return expanded;
}
