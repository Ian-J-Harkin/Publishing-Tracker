import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { PlatformService } from '../../../core/services/platform.service';
import { PlatformRequest } from '../../../core/models/platform';

@Component({
  selector: 'app-request-platform',
  standalone: true,
  imports: [CommonModule, RouterModule, ReactiveFormsModule],
  templateUrl: './request-platform.component.html',
  styleUrls: ['./request-platform.component.css']
})
export class RequestPlatformComponent {
  requestPlatformForm: FormGroup;
  error: string | null = null;

  constructor(
    private fb: FormBuilder,
    private platformService: PlatformService,
    private router: Router
  ) {
    this.requestPlatformForm = this.fb.group({
      name: ['', Validators.required],
      baseUrl: [''],
      commissionRate: [0, Validators.required]
    });
  }

  onSubmit(): void {
    if (this.requestPlatformForm.valid) {
      const newPlatformRequest: PlatformRequest = this.requestPlatformForm.value;
      this.platformService.requestPlatform(newPlatformRequest).subscribe({
        next: () => {
          this.router.navigate(['/platforms']);
        },
        error: (err) => {
          this.error = 'Failed to submit request.';
        }
      });
    }
  }
}