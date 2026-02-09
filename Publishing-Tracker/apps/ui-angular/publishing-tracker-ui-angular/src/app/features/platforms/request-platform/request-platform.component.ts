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
  submissionStatus: 'idle' | 'success' | 'error' = 'idle';
  errorMessage: string | null = null;

  constructor(
    private fb: FormBuilder,
    private platformService: PlatformService,
    private router: Router
  ) {
    this.requestPlatformForm = this.fb.group({
      name: ['', [Validators.required, Validators.minLength(2)]],
      baseUrl: ['', [Validators.pattern('https?://.+')]],
      commissionRate: [0.30, [Validators.required, Validators.min(0), Validators.max(1)]]
    });
  }

  onSubmit(): void {
    if (this.requestPlatformForm.valid) {
      this.submissionStatus = 'idle';
      const newPlatformRequest: PlatformRequest = this.requestPlatformForm.value;

      this.platformService.requestPlatform(newPlatformRequest).subscribe({
        next: () => {
          this.submissionStatus = 'success';
          setTimeout(() => this.router.navigate(['/platforms']), 2000);
        },
        error: (err) => {
          this.submissionStatus = 'error';
          this.errorMessage = 'Network Failure: Unable to secure the request at this time.';
        }
      });
    }
  }

  discard(): void {
    this.router.navigate(['/platforms']);
  }
}