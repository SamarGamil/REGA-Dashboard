import { Component } from '@angular/core';
import { CommonModule, Location } from '@angular/common';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-not-found',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './not-found.component.html',
})
export class NotFoundComponent {
  currentPath: string;

  constructor(private location: Location) {
    this.currentPath = this.location.path();
  }
}
