import { Component, type ReactNode } from 'react';

interface State {
  error: Error | null;
}

export class ErrorBoundary extends Component<{ children: ReactNode }, State> {
  state: State = { error: null };

  static getDerivedStateFromError(error: Error): State {
    return { error };
  }

  render() {
    if (!this.state.error) return this.props.children;
    return (
      <main className="picker">
        <h1>Something went wrong</h1>
        <p className="muted center">{this.state.error.message}</p>
        <button type="button" className="button button-primary" onClick={() => window.location.assign('/')}>
          Reload Tally
        </button>
      </main>
    );
  }
}
