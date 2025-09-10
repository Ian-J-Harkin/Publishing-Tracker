/// <reference types="cypress" />

describe('Advanced Analytics', () => {
    beforeEach(() => {
        cy.login('test@test.com', 'password123');
    });

    it('should display advanced analytics on the dashboard', () => {
        cy.visit('/dashboard');
        cy.contains('Year-Over-Year Growth').should('be.visible');
        cy.contains('Seasonal Performance').should('be.visible');
    });

    it('should display book performance data', () => {
        cy.visit('/books/1/performance');
        cy.contains('Book Performance').should('be.visible');
    });
});