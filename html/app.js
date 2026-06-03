$(document).ready(function() {
    // Smooth scrolling
    $('a[href^="#"]').on('click', function(e) {
        e.preventDefault();
        var target = this.hash;
        var $target = $(target);
        $('html, body').stop().animate({
            'scrollTop': $target.offset().top - 80
        }, 900, 'swing');
    });

    // Handle Search Bar Focus
    $('#service-search').on('focus', function() {
        $('.search-box').css('border-color', '#4F46E5');
    }).on('blur', function() {
        $('.search-box').css('border-color', '#E2E8F0');
    });

    // Category Card Click
    $('.category-card').on('click', function() {
        const service = $(this).data('service');
        $('#service-search').val(service);
        openRequestModal(service);
    });

    // Start Request Button
    $('#start-request').on('click', function() {
        const service = $('#service-search').val();
        openRequestModal(service);
    });

    function openRequestModal(service) {
        if (service) {
            $('#job-desc').val(`I need help with ${service.toLowerCase()}...`);
        }
        $('#request-modal').css('display', 'flex').hide().fadeIn(300);
        $('body').css('overflow', 'hidden');
    }

    // Close Modal
    $('.close-modal, .modal-overlay').on('click', function(e) {
        if (e.target !== this) return;
        $('#request-modal').fadeOut(300, function() {
            resetModal();
        });
        $('body').css('overflow', 'auto');
    });

    function resetModal() {
        $('.request-step').removeClass('active');
        $('#step-1').addClass('active');
        $('#live-bids').empty().append('<div class="empty-bids"><p>Waiting for the first bid...</p></div>');
    }

    // Next Step: Start Bidding Simulation
    $('#next-to-bids').on('click', function() {
        $('#step-1').removeClass('active');
        $('#step-2').addClass('active');
        
        // Simulate bids coming in
        startBiddingSimulation();
    });

    const mockProviders = [
        { name: "Johan's Plumbing", rating: 4.8, jobs: 152, price: 450, eta: 25, initial: "JP" },
        { name: "Sipho Electrical", rating: 4.9, jobs: 89, price: 380, eta: 15, initial: "SE" },
        { name: "Thabo Repairs", rating: 4.7, jobs: 210, price: 400, eta: 40, initial: "TR" },
        { name: "CleanPro SA", rating: 4.6, jobs: 340, price: 320, eta: 60, initial: "CP" }
    ];

    function startBiddingSimulation() {
        let bidCount = 0;
        const interval = setInterval(() => {
            if (bidCount >= mockProviders.length) {
                clearInterval(interval);
                $('.loader-ring').fadeOut();
                $('.bidding-header p').text('All bids received. Choose your pro!');
                return;
            }

            const provider = mockProviders[bidCount];
            addBid(provider);
            bidCount++;
        }, 2000);
    }

    function addBid(provider) {
        if ($('.empty-bids').length) $('.empty-bids').remove();

        const bidHtml = `
            <div class="bid-card">
                <div class="bid-provider">
                    <div class="provider-avatar">${provider.initial}</div>
                    <div class="bid-info">
                        <h4>${provider.name}</h4>
                        <div class="bid-rating">
                            <i class="fa-solid fa-star"></i> ${provider.rating} (${provider.jobs} jobs)
                        </div>
                    </div>
                </div>
                <div class="bid-amount">
                    <span class="price">R${provider.price}</span>
                    <span class="eta">ETA: ${provider.eta} mins</span>
                    <button class="btn btn-primary btn-sm accept-bid" style="margin-top:8px; padding: 6px 12px; font-size: 0.8rem;">Accept</button>
                </div>
            </div>
        `;

        $('#live-bids').append(bidHtml);
    }

    // Accept Bid Interaction
    $(document).on('click', '.accept-bid', function() {
        const providerName = $(this).closest('.bid-card').find('h4').text();
        alert(`Great choice! ${providerName} is on their way. (This is a UI prototype)`);
        $('#request-modal').fadeOut();
        $('body').css('overflow', 'auto');
    });

    // Auth Role Toggles
    $('.auth-tab').on('click', function() {
        const role = $(this).data('role');
        const parent = $(this).parent();
        
        parent.find('.auth-tab').removeClass('active');
        $(this).addClass('active');

        if (parent.attr('id') === 'register-tabs') {
            if (role === 'provider') {
                $('#provider-fields').addClass('active');
            } else {
                $('#provider-fields').removeClass('active');
            }
        } else if (parent.attr('id') === 'login-tabs') {
            if (role === 'admin') {
                $('.social-auth, .divider').hide();
                $('.auth-header p').text('Enter your administrator credentials.');
            } else {
                $('.social-auth, .divider').show();
                $('.auth-header p').text('Login to manage your services or requests.');
            }
        }
    });

    // Form Submission Simulation
    $('#login-form, #register-form').on('submit', function(e) {
        e.preventDefault();
        const isLogin = $(this).attr('id') === 'login-form';
        const type = isLogin ? 'Login' : 'Registration';
        
        let redirectUrl = 'index.html';
        if (isLogin) {
            const activeRole = $('#login-tabs .auth-tab.active').data('role');
            if (activeRole === 'admin') {
                redirectUrl = 'dashboard-admin.html';
            } else {
                // For simulation: if email contains 'pro' go to provider, else customer
                const email = $(this).find('input[type="email"]').val().toLowerCase();
                redirectUrl = email.includes('pro') ? 'dashboard-provider.html' : 'dashboard-customer.html';
            }
        }

        alert(`${type} successful! redirecting to dashboard... (UI Simulation)`);
        window.location.href = redirectUrl;
    });

    // Quick Simulation Buttons
    $('.sim-login').on('click', function() {
        const target = $(this).data('target');
        alert(`Simulating login... redirecting to ${target}`);
        window.location.href = target;
    });

    // Admin Verification Simulation
    $(document).on('click', '.accept-verif', function() {
        const row = $(this).closest('tr');
        const name = row.find('td strong').text();
        row.find('.status-badge').removeClass('status-pending').addClass('status-completed').text('Approved');
        $(this).parent().html('<span style="color:var(--secondary); font-weight:600;"><i class="fa-solid fa-check"></i> Verified</span>');
        alert(`Provider "${name}" has been verified.`);
    });

    // Dashboard Navigation (Reverted to physical pages for reliability)
    /*
    $('.sidebar-nav .nav-item').on('click', function(e) {
        ...
    });
    */

    // Chat simulation
    $('#chat-send').on('click', function() {
        const msg = $('#chat-msg-input').val();
        if (!msg) return;

        $('.chat-messages').append(`<div class="message sent">${msg}</div>`);
        $('#chat-msg-input').val('');
        $('.chat-messages').scrollTop($('.chat-messages')[0].scrollHeight);

        setTimeout(() => {
            $('.chat-messages').append(`<div class="message received">Thanks! I'll be there in 15 minutes.</div>`);
            $('.chat-messages').scrollTop($('.chat-messages')[0].scrollHeight);
        }, 1500);
    });

});
