import React, { useState } from 'react';

import { useAuth } from '../context/AuthContext';
import { useNavigate } from 'react-router-dom';
import '../pages/LoginPage.css';
import { useDependencies } from '../context/DependencyContext';
import { getApiErrorMessage } from '../utils/apiError';
import { LoginRequest } from '../types/auth';

const LoginPage = () => {
    const { auth: authPort } = useDependencies();
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
            const credentials: LoginRequest = { email, password };
            const response = await authPort.login(credentials);
            console.log('Login response:', response);

            if (!response.token) {
                throw new Error('No token received from server');
            }

            login(response.token);
            navigate('/dashboard');
        } catch (err: unknown) {
            const errorMsg = getApiErrorMessage(err, 'Login failed');
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

