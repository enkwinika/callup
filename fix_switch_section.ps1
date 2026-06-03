$f = 'Views\Shared\_Layout.cshtml'
$c = [System.IO.File]::ReadAllText((Resolve-Path $f))

$newFunc = @'
        window.switchSection = function(id, element) {
            var $target = $('#view-' + id);
            if ($target.length === 0) {
                console.warn('View section not found: ' + id);
                return;
            }

            $('.view-section').removeClass('active');
            $target.addClass('active');

            // Unified nav highlighting (sidebar + tabs)
            $('.nav-item').removeClass('active');
            $(element).addClass('active');

            // Ensure sidebar nav matches if a content tab was clicked
            if (!$(element).closest('.sidebar').length) {
                $('.sidebar .nav-item[onclick*="'+id+'"]').addClass('active');
            }

            // ── Dynamic page title ─────────────────────────────
            var menuText = $(element).find('span').text().trim() || $(element).text().trim();
            if ($('#pageTitle').length && menuText) {
                $('#pageTitle').text(menuText);
            }
            if ($('#pageSubtitle').length) {
                $('#pageSubtitle').toggle(id === 'requests');
            }
            if ($('#newRequestBtn').length) {
                $('#newRequestBtn').toggle(id === 'requests' || id === 'find-service');
            }

            if (window.innerWidth <= 1024) closeSidebar();
        };
'@

# Use a regex to replace the entire switchSection function
$pattern = '(?s)window\.switchSection = function\(id, element\) \{.*?\};'
$result = [System.Text.RegularExpressions.Regex]::Replace($c, $pattern, $newFunc.TrimStart())
[System.IO.File]::WriteAllText((Resolve-Path $f), $result, [System.Text.Encoding]::UTF8)
Write-Host "Done"
