export interface BackdropNode {
  x: number;
  y: number;
  vx: number;
  vy: number;
  r: number;
}

export interface BackdropColors {
  /** Link lines, as an `r, g, b` triplet; alpha is applied per line. */
  link: string;
  /** Node dots, as an `r, g, b` triplet. */
  dot: string;
}
