import React, { useState } from 'react';
import authService from '../services/authService';
import { useAuth } from '../context/AuthContext';
import { useNavigate } from 'react-router-dom';

const LoginPage = () => {
    const [email, setEmail] = useState('');
    const [password, setPassword] = useState('');
    const { login } = useAuth();
    const navigate = useNavigate();

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        try {
            const response = await authService.login({ email, password });
            login(response.data.token);
            navigate('/dashboard');
        } catch (error) {
            console.error(error);
        }
    };

    return (
        <div>
            <h2>Login</h2>
            <form onSubmit={handleSubmit}>
                <div>
                    <label>Email</label>
                    <input type="email" value={email} onChange={(e) => setEmail(e.target.value)} data-cy="email-input" />
                </div>
                <div>
                    <label>Password</label>
                    <input type="password" value={password} onChange={(e) => setPassword(e.target.value)} data-cy="password-input" />
                </div>
                <button type="submit" data-cy="login-button">Login</button>
            </form>
        </div>
    );
};

export default LoginPage;