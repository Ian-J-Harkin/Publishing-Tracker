import React, { useState } from 'react';
import authService from '../services/authService';
import { useAuth } from '../context/AuthContext';
import { useNavigate } from 'react-router-dom';
import '../pages/LoginPage.css';

const LoginPage = () => {
    const [email, setEmail] = useState('');
    const [password, setPassword] = useState('');
    const [error, setError] = useState('');
    const [loading, setLoading] = useState(false);
    const { login } = useAuth();
    const navigate = useNavigate();

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        setError('');
        setLoading(true);
        try {
            console.log('Attempting login with:', { email });
            const response = await authService.login({ email, password });
            console.log('Login response:', response.data);

            if (!response.data.token) {
                throw new Error('No token received from server');
            }

            login(response.data.token);
            navigate('/dashboard');
        } catch (err: unknown) {
            let errorMsg = 'Login failed';

            if (err && typeof err === 'object') {
                const axiosErr = err as { response?: { data?: { message?: string } } };
                if (axiosErr.response?.data?.message) {
                    errorMsg = axiosErr.response.data.message;
                } else if (err instanceof Error) {
                    errorMsg = err.message;
                }
            }

            console.error('Login error:', err);
            setError(errorMsg);
        } finally {
            setLoading(false);
        }
    };

    return (
        <div className="login-container">
            <div className="login-card">
                <h2 className="login-title">Welcome Back</h2>

                {error && <div style={{ color: 'red', marginBottom: '1rem' }}>{error}</div>}

                <form onSubmit={handleSubmit} className="login-form">
                    <div className="form-group">
                        <label>Email</label>
                        <input
                            type="email"
                            value={email}
                            onChange={(e) => setEmail(e.target.value)}
                            disabled={loading}
                            data-cy="email-input"
                        />
                    </div>

                    <div className="form-group">
                        <label>Password</label>
                        <input
                            type="password"
                            value={password}
                            onChange={(e) => setPassword(e.target.value)}
                            disabled={loading}
                            data-cy="password-input"
                        />
                    </div>

                    <button type="submit" className="login-button" disabled={loading} data-cy="login-button">
                        {loading ? 'Logging in...' : 'Login'}
                    </button>
                </form>
            </div>
        </div>
    );
};

export default LoginPage;



