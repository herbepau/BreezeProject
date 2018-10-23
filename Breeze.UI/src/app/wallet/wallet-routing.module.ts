import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';

import { WalletComponent }   from './wallet.component';
import { HistoryComponent } from './history/history.component';
import { TumblebitComponent } from './tumblebit/tumblebit.component';
import { StratisTumblebitComponent } from './tumblebit/stratis-tumblebit.component';
import { DashboardComponent } from './dashboard/dashboard.component';
import { AdvancedComponent } from './advanced/advanced.component';

const routes: Routes = [
  { path: '', component: WalletComponent,
    children: [
      { path: '', redirectTo:'dashboard', pathMatch:'full' },
      { path: 'dashboard', component: DashboardComponent, data: { shouldReuse: false } },
      { path: 'privacy', component: TumblebitComponent, data: { shouldReuse: true } },
      { path: 'history', component: HistoryComponent, data: { shouldReuse: false } },
      { path: 'advanced', component: AdvancedComponent }
    ]
  },
  { path: 'stratis-wallet', component: WalletComponent,
  children: [
    { path: '', redirectTo:'dashboard', pathMatch:'full' },
    { path: 'dashboard', component: DashboardComponent, data: { shouldReuse: false } },
    { path: 'history', component: HistoryComponent, data: { shouldReuse: false } },
    { path: 'privacy-strat', component: StratisTumblebitComponent, data: { shouldReuse: true } },
    { path: 'advanced', component: AdvancedComponent }
  ]
}
];

@NgModule({
  imports: [ RouterModule.forChild(routes) ],
  exports: [ RouterModule ]
})

export class WalletRoutingModule {}
