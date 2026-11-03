package com.ankitsharma.utsavnights.photo;

import androidx.core.content.FileProvider;

/** Its own class, so its manifest entry doesn't merge with the clips library's FileProvider. */
public final class PhotoFileProvider extends FileProvider {
}
