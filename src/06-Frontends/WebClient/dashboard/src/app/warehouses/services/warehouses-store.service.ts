import { Injectable, OnDestroy } from '@angular/core';
import { BehaviorSubject } from 'rxjs';
import { Warehouse, WarehouseCreateRequest } from '../models/warehouses.model';
import { WarehousesBackendService } from './warehouses-backend.service';
import { NotificationStoreService } from 'src/app/shared/services/notification-store.service';
import { handleHttpError } from 'src/app/shared/helpers/http/http-error.helpers';
import { NotificationLevel } from 'src/app/shared/models/notification.model';
import { filter } from 'rxjs/operators';
import { guid } from 'src/app/shared/types/guid.type';

@Injectable({
	providedIn: 'root'
})
export class WarehousesStoreService implements OnDestroy {

	private _warehouses: BehaviorSubject<Warehouse[]> = new BehaviorSubject([]);

	constructor(
		private backend: WarehousesBackendService,
		private notification: NotificationStoreService,
	) { }

	ngOnDestroy() {
		// needed by untilDestroyed
	}

	get warehouses$() {
		return this._warehouses.asObservable();
	}

	getWarehouses() {
		const items = this.backend.getWarehouses();
		items.subscribe(result => {
			this._warehouses.next(result.warehouses);
		}, error => {
			handleHttpError(error, this.notification);
			this._warehouses.next([]);
		});
	}

	getInventory(warehouseGuid: guid) {
		const items = this.backend.getInventory(warehouseGuid);
		items.subscribe(inventory => {
			const warehouses = this._warehouses.getValue();
			const idx = warehouses.findIndex(w => w.id === warehouseGuid);
			warehouses[idx].inventory = inventory;
			this._warehouses.next(warehouses);
		}, error => {
			handleHttpError(error, this.notification);
			// this._warehouses.next([]);
		});
	}

	async createWarehouse(request: WarehouseCreateRequest) {
		try {
			const response = await this.backend.createWarehouse(request);
			this.notification.dispatch(NotificationLevel.success, 'Warehouse created');

			const warehouses = this._warehouses.getValue();
			warehouses.push(response);
			this._warehouses.next(warehouses);
			return true;
		} catch (error) {
			handleHttpError(error, this.notification);
		}
	}
}
