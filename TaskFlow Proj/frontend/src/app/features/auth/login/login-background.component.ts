import { AfterViewInit, Component, ElementRef, NgZone, OnDestroy, ViewChild, inject } from '@angular/core';

interface Node {
  x: number;
  y: number;
  vx: number;
  vy: number;
  r: number;
}

const LINK_DISTANCE = 140;
const MAX_NODES = 70;
const AREA_PER_NODE = 14000;
const MAX_PIXEL_RATIO = 2;

/**
 * Decorative full-screen particle network behind the login card. It is hidden from assistive tech, ignores
 * pointer input, stays still under reduced motion, pauses while the tab is hidden, and stops on destroy.
 */
@Component({
  selector: 'app-login-background',
  templateUrl: './login-background.component.html',
  styleUrl: './login-background.component.css'
})
export class LoginBackgroundComponent implements AfterViewInit, OnDestroy {
  private readonly zone = inject(NgZone);

  @ViewChild('canvas', { static: true }) private readonly canvasRef!: ElementRef<HTMLCanvasElement>;

  private ctx: CanvasRenderingContext2D | null = null;
  private nodes: Node[] = [];
  private frame = 0;
  private width = 0;
  private height = 0;
  private reducedMotion: MediaQueryList | null = null;

  private readonly onResize = () => this.resize();
  private readonly onVisibility = () => (document.hidden ? this.stop() : this.start());
  private readonly onMotionChange = () => this.syncMotion();

  ngAfterViewInit(): void {
    this.ctx = this.canvasRef.nativeElement.getContext('2d');
    this.reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)');
    this.resize();
    window.addEventListener('resize', this.onResize);
    document.addEventListener('visibilitychange', this.onVisibility);
    this.reducedMotion.addEventListener('change', this.onMotionChange);
    this.syncMotion();
  }

  ngOnDestroy(): void {
    this.stop();
    window.removeEventListener('resize', this.onResize);
    document.removeEventListener('visibilitychange', this.onVisibility);
    this.reducedMotion?.removeEventListener('change', this.onMotionChange);
  }

  /** Whether the animation loop is currently scheduled. */
  get running(): boolean {
    return this.frame !== 0;
  }

  private syncMotion(): void {
    const canvas = this.canvasRef.nativeElement;
    if (this.reducedMotion?.matches) {
      this.stop();
      canvas.hidden = true;
    } else {
      canvas.hidden = false;
      this.start();
    }
  }

  private start(): void {
    if (this.frame !== 0 || document.hidden || !this.ctx || this.reducedMotion?.matches) {
      return;
    }
    this.zone.runOutsideAngular(() => {
      this.frame = requestAnimationFrame(() => this.tick());
    });
  }

  private stop(): void {
    if (this.frame !== 0) {
      cancelAnimationFrame(this.frame);
      this.frame = 0;
    }
  }

  private resize(): void {
    const canvas = this.canvasRef.nativeElement;
    const ratio = Math.min(window.devicePixelRatio || 1, MAX_PIXEL_RATIO);
    this.width = window.innerWidth;
    this.height = window.innerHeight;
    canvas.width = this.width * ratio;
    canvas.height = this.height * ratio;
    this.ctx?.setTransform(ratio, 0, 0, ratio, 0, 0);

    const target = Math.min(MAX_NODES, Math.round((this.width * this.height) / AREA_PER_NODE));
    while (this.nodes.length < target) {
      this.nodes.push({
        x: Math.random() * this.width,
        y: Math.random() * this.height,
        vx: (Math.random() - 0.5) * 0.35,
        vy: (Math.random() - 0.5) * 0.35,
        r: 1.2 + Math.random() * 1.6
      });
    }
    this.nodes.length = target;
  }

  private tick(): void {
    const ctx = this.ctx;
    if (!ctx) {
      return;
    }
    ctx.clearRect(0, 0, this.width, this.height);
    for (let i = 0; i < this.nodes.length; i++) {
      const a = this.nodes[i];
      a.x += a.vx;
      a.y += a.vy;
      if (a.x < 0 || a.x > this.width) {
        a.vx *= -1;
      }
      if (a.y < 0 || a.y > this.height) {
        a.vy *= -1;
      }
      for (let j = i + 1; j < this.nodes.length; j++) {
        const b = this.nodes[j];
        const distance = Math.hypot(a.x - b.x, a.y - b.y);
        if (distance < LINK_DISTANCE) {
          ctx.strokeStyle = `rgba(147, 197, 253, ${(0.28 * (1 - distance / LINK_DISTANCE)).toFixed(3)})`;
          ctx.lineWidth = 1;
          ctx.beginPath();
          ctx.moveTo(a.x, a.y);
          ctx.lineTo(b.x, b.y);
          ctx.stroke();
        }
      }
      ctx.fillStyle = 'rgba(191, 219, 254, 0.7)';
      ctx.beginPath();
      ctx.arc(a.x, a.y, a.r, 0, Math.PI * 2);
      ctx.fill();
    }
    this.frame = requestAnimationFrame(() => this.tick());
  }
}
