import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { PlatformService } from '../../core/services/platform.service';
import { Platform } from '../../core/models/platform';
import { Observable } from 'rxjs';

@Component({
  selector: 'app-platforms',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './platforms.component.html',
  styleUrls: ['./platforms.component.css']
})
export class PlatformsComponent implements OnInit {
  platforms$!: Observable<Platform[]>;
  error: string | null = null;

  constructor(private platformService: PlatformService) { }

  ngOnInit(): void {
    this.platforms$ = this.platformService.getPlatforms();
  }
}