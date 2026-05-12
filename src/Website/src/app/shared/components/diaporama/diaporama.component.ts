import {Component, input, OnDestroy, OnInit, signal} from '@angular/core';

@Component({
  selector: 'app-diaporama',
  templateUrl: './diaporama.component.html',
  styleUrl: './diaporama.component.scss'
})
export class DiaporamaComponent implements OnInit, OnDestroy {
  constructor() {
  }

  pictureList = input<string[]>([]);
  currentPicture = signal(0);
  private interactionTimeoutId: any;
  private shouldAutoSlide = signal(true)
  private intervalId: any;

  ngOnDestroy(): void {
    this.stopSlideshow();
    clearTimeout(this.interactionTimeoutId);
  }

  private stopSlideshow(): void {
    if (this.intervalId) {
      clearInterval(this.intervalId);
    }
  }

  private startSlideshow(): void {
    this.intervalId = setInterval(() => {
      this.currentPicture.set((this.currentPicture() + 1) % this.pictureList().length);
    }, 3000);
  }

  private setUserInteraction(): void {
    this.shouldAutoSlide.set(false);
    this.stopSlideshow();
    clearTimeout(this.interactionTimeoutId);
    this.interactionTimeoutId = setTimeout(() => {
      this.shouldAutoSlide.set(true);
      this.startSlideshow();
    }, 10000);
  }

  next() {
    this.currentPicture.set((this.currentPicture() + 1) % this.pictureList().length);
    this.setUserInteraction();
  }

  previous() {
    this.currentPicture.set((this.currentPicture() - 1 + this.pictureList().length) % this.pictureList().length);
    this.setUserInteraction();
  }

  ngOnInit(): void {
    this.startSlideshow();
  }
}
