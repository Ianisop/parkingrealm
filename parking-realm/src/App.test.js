import { render, screen } from '@testing-library/react';
import App from './App';

test('renders the registration page', () => {
  render(<App />);
  expect(screen.getByRole('heading', { name: 'Opret bruger' })).toBeInTheDocument();
});
