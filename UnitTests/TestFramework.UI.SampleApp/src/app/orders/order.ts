/** One order, as the sample API returns it. */
export interface Order {
  readonly id: string;
  readonly product: string;
  readonly quantity: number;
  readonly price: number;
  readonly status: string;
}
