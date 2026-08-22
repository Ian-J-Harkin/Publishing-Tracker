import React, { useState } from 'react';

import { useNavigate } from 'react-router-dom';
import { useDependencies } from '../context/DependencyContext';
import { getApiErrorMessage } from '../utils/apiError';
import { RegisterRequest } from '../types/auth';

const RegisterPage = () => {
    const { auth: authPort } = useDependencies();
    const [email, setEmail] = useState('');
    const [password, setPassword] = useState('');
    const [firstName, setFirstName] = useState('');
    const [lastName, setLastName] = useState('');
    const [error, setError] = useState('');
    const navigate = useNavigate();

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        setError('');
        try {
            const payload: RegisterRequest = { firstName, lastName, email, password };
            await authPort.register(payload);
            navigate('/login');
        } catch (error) {
            setError(getApiErrorMessage(error, 'Registration failed'));
        }
    };

    return (
        <div>
            <h2>Register</h2>
            {error && <div style={{ color: 'red', marginBottom: '1rem' }}>{error}</div>}
            <form onSubmit={handleSubmit}>
                <div>
                    <label>First Name</label>
                    <input type="text" value={firstName} onChange={(e) => setFirstName(e.target.value)} />
                </div>
                <div>
                    <label>Last Name</label>
                    <input type="text" value={lastName} onChange={(e) => setLastName(e.target.value)} />
                </div>
                <div>
                    <label>Email</label>
                    <input type="email" value={email} onChange={(e) => setEmail(e.target.value)} />
                </div>
                <div>
                    <label>Password</label>
                    <input type="password" value={password} onChange={(e) => setPassword(e.target.value)} />
                </div>
                <button type="submit">Register</button>
            </form>
        </div>
    );
};

export default RegisterPage;
