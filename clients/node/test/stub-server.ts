/**
 * Stands in for a FeatureFlags installation. Records what it was asked, because the request is half
 * of what this package does — the bearer token and the `If-None-Match` are the whole of the
 * conditional-fetch contract.
 */
export interface StubRequest {
  url: string;
  headers: Headers;
}

export class StubServer {
  readonly requests: StubRequest[] = [];

  private readonly answers: Array<() => Response | Promise<Response>> = [];
  private last: (() => Response | Promise<Response>) | null = null;

  /** How long the stub takes to answer. Stands in for a server that accepts and then goes quiet. */
  delay = 0;

  get callCount(): number {
    return this.requests.length;
  }

  answers_(answer: () => Response | Promise<Response>): this {
    this.answers.push(answer);

    return this;
  }

  withFlags(flags: Record<string, boolean>, etag: string, environment = 'dev'): this {
    return this.answers_(
      () =>
        new Response(JSON.stringify({ environment, flags }), {
          status: 200,
          headers: { 'content-type': 'application/json', etag },
        }),
    );
  }

  notModified(): this {
    return this.answers_(() => new Response(null, { status: 304 }));
  }

  withStatus(status: number, body?: unknown): this {
    return this.answers_(
      () =>
        new Response(body === undefined ? null : JSON.stringify(body), {
          status,
          headers: body === undefined ? undefined : { 'content-type': 'application/json' },
        }),
    );
  }

  unreachable(): this {
    return this.answers_(() => {
      throw new TypeError('fetch failed');
    });
  }

  /** The fetch to hand the client. The last answer queued repeats once the queue runs dry. */
  get fetch(): typeof globalThis.fetch {
    return async (input: RequestInfo | URL, init?: RequestInit): Promise<Response> => {
      this.requests.push({
        url: String(input),
        headers: new Headers(init?.headers),
      });

      if (this.delay > 0) {
        await new Promise((resolve, reject) => {
          const timer = setTimeout(resolve, this.delay);

          init?.signal?.addEventListener('abort', () => {
            clearTimeout(timer);
            reject(init.signal?.reason ?? new Error('aborted'));
          });
        });
      }

      const answer = this.answers.shift() ?? this.last;

      if (!answer) {
        throw new Error('The stub was asked before it was told what to say.');
      }

      this.last = answer;

      return answer();
    };
  }
}
