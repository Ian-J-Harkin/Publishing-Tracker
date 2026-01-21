import { render, screen } from '@testing-library/react';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import ProtectedRoute from './ProtectedRoute';
import { AuthProvider } from '../context/AuthContext';

const TestComponent = () => <div>Test Component</div>;

describe('ProtectedRoute', () => {
    it('renders the component when authenticated', () => {
        localStorage.setItem('token', 'test-token');
        render(
            <AuthProvider>
                <MemoryRouter initialEntries={['/protected']}>
                    <Routes>
                        <Route
                            path="/protected"
                            element={
                                <ProtectedRoute>
                                    <TestComponent />
                                </ProtectedRoute>
                            }
                        />
                    </Routes>
                </MemoryRouter>
            </AuthProvider>
        );
        expect(screen.getByText('Test Component')).toBeInTheDocument();
    });

    it('redirects to the login page when not authenticated', () => {
        localStorage.removeItem('token');
        render(
            <AuthProvider>
                <MemoryRouter initialEntries={['/protected']}>
                    <Routes>
                        <Route
                            path="/protected"
                            element={
                                <ProtectedRoute>
                                    <TestComponent />
                                </ProtectedRoute>
                            }
                        />
                        <Route path="/login" element={<div>Login Page</div>} />
                    </Routes>
                </MemoryRouter>
            </AuthProvider>
        );
        expect(screen.getByText('Login Page')).toBeInTheDocument();
    });
});