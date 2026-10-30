import { Component, Output, EventEmitter } from '@angular/core';
import { CoefficientResult } from '../../../../core/services/api/manufacturing-api.service';
import { CoefficientDeclarationComponent } from '../coefficient-declaration/coefficient-declaration.component';
@Component({ selector: 'app-bom-list', standalone: true, imports: [CoefficientDeclarationComponent],
  template: `<app-coefficient-declaration [readOnly]="true" (declarationSelected)="declarationSelected.emit($event)"></app-coefficient-declaration>` })
export class BomListComponent { @Output() declarationSelected = new EventEmitter<CoefficientResult>(); }
