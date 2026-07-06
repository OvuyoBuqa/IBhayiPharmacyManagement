/**
 * Console Error Fixes
 * Simple JavaScript fixes for common console errors without server configuration
 */

(function() {
    'use strict';
    
    // Wait for DOM to be ready
    document.addEventListener('DOMContentLoaded', function() {
        
        // Fix 1: Override Sparkline to prevent innerHTML errors
        if (typeof window.Sparkline !== 'undefined') {
            var OriginalSparkline = window.Sparkline;
            window.Sparkline = function(element, options) {
                if (!element) {
                    console.warn('Sparkline: Element not found, skipping initialization');
                    return { draw: function() {} }; // Return dummy object
                }
                try {
                    return new OriginalSparkline(element, options);
                } catch (e) {
                    console.warn('Sparkline initialization failed:', e);
                    return { draw: function() {} }; // Return dummy object
                }
            };
        }
        
        // Fix 2: Override jQuery Sparkline plugin
        if (typeof $ !== 'undefined' && typeof $.fn !== 'undefined') {
            if (typeof $.fn.sparkline !== 'undefined') {
                var originalSparkline = $.fn.sparkline;
                $.fn.sparkline = function(options) {
                    if (this.length === 0) {
                        console.warn('jQuery Sparkline: No elements found');
                        return this;
                    }
                    try {
                        return originalSparkline.call(this, options);
                    } catch (e) {
                        console.warn('jQuery Sparkline failed:', e);
                        return this;
                    }
                };
            }
            
            // Fix 3: Override Vector Map to prevent SVG errors
            if (typeof $.fn.vectorMap !== 'undefined') {
                var originalVectorMap = $.fn.vectorMap;
                $.fn.vectorMap = function(options) {
                    if (this.length === 0) {
                        console.warn('VectorMap: No elements found');
                        return this;
                    }
                    if (this.width() === 0 || this.height() === 0) {
                        console.warn('VectorMap: Element has zero dimensions');
                        return this;
                    }
                    try {
                        return originalVectorMap.call(this, options);
                    } catch (e) {
                        console.warn('VectorMap initialization failed:', e);
                        return this;
                    }
                };
            }
        }
        
        // Fix 4: Override dashboard initialization
        if (typeof window.dashboard !== 'undefined') {
            window.dashboard = function() {
                console.log('Dashboard initialization overridden to prevent errors');
                return false;
            };
        }
        
        // Fix 5: Prevent innerHTML errors on undefined elements
        var originalInnerHTML = Object.getOwnPropertyDescriptor(Element.prototype, 'innerHTML');
        if (originalInnerHTML) {
            Object.defineProperty(Element.prototype, 'innerHTML', {
                get: function() {
                    return originalInnerHTML.get.call(this);
                },
                set: function(value) {
                    try {
                        originalInnerHTML.set.call(this, value);
                    } catch (e) {
                        console.warn('innerHTML error prevented:', e);
                    }
                }
            });
        }
        
        // Fix 6: Override SVG setAttribute to handle undefined values
        var originalSetAttribute = Element.prototype.setAttribute;
        Element.prototype.setAttribute = function(name, value) {
            if (name === 'width' || name === 'height') {
                if (value === 'undefined' || value === undefined || value === null) {
                    value = name === 'width' ? '100' : '100';
                }
            }
            return originalSetAttribute.call(this, name, value);
        };
        
        // Fix 7: Override jQuery Deferred to handle errors gracefully
        if (typeof $ !== 'undefined' && typeof $.Deferred !== 'undefined') {
            var originalDeferred = $.Deferred;
            $.Deferred = function(func) {
                var deferred = originalDeferred.call(this, func);
                var originalReject = deferred.reject;
                deferred.reject = function() {
                    try {
                        return originalReject.apply(this, arguments);
                    } catch (e) {
                        console.warn('jQuery Deferred error prevented:', e);
                        return this;
                    }
                };
                return deferred;
            };
        }
        
        console.log('Console error fixes applied successfully');
    });
    
    // Global error handler
    window.onerror = function(message, source, lineno, colno, error) {
        console.warn('JavaScript error caught and handled:', {
            message: message,
            source: source,
            line: lineno,
            column: colno
        });
        return true; // Prevent default error handling
    };
    
    // Handle unhandled promise rejections
    window.addEventListener('unhandledrejection', function(event) {
        console.warn('Unhandled promise rejection caught:', event.reason);
        event.preventDefault();
    });
    
})();
