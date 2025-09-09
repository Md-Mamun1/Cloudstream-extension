package com.lagradost.cloudstream3

import com.lagradost.cloudstream3.utils.*
import org.jsoup.nodes.Element

class FtpBdProvider : MainPlugin() {
    override var mainUrl = "https://ftpbd.net"
    override var name = "FtpBd"
    override var lang = "bn"
    override val hasMainPage = true
    override val hasDownloadSupport = false
    override val supportedTypes = setOf(
        TvType.Movie,
        TvType.TvSeries,
        TvType.AsianDrama
    )

    override val mainPage = mainPageOf(
        "$mainUrl/movie/" to "Movies",
        "$mainUrl/series/" to "TV Series",
        "$mainUrl/drama/" to "Dramas",
        "$mainUrl/hollywood-movie/" to "Hollywood Movies",
        "$mainUrl/hindi-movie/" to "Hindi Movies",
        "$mainUrl/tamil-movie/" to "Tamil Movies",
        "$mainUrl/telugu-movie/" to "Telugu Movies"
    )

    override suspend fun getMainPage(
        page: Int,
        request: MainPageRequest
    ): HomePageResponse {
        val url = request.data + if (page > 1) "page/$page/" else ""
        val document = app.get(url).document
        val home = document.select("article.item").mapNotNull {
            it.toSearchResult()
        }
        return newHomePageResponse(request.name, home)
    }

    private fun Element.toSearchResult(): SearchResponse? {
        val title = this.selectFirst("h3 a")?.text() ?: return null
        val href = this.selectFirst("h3 a")?.attr("href") ?: return null
        val posterUrl = this.selectFirst("img")?.attr("src")
        val quality = getQualityFromString(this.selectFirst(".quality")?.text())

        val type = when {
            href.contains("/series/", true) -> TvType.TvSeries
            href.contains("/drama/", true) -> TvType.AsianDrama
            else -> TvType.Movie
        }

        return newMovieSearchResponse(title, href, type) {
            this.posterUrl = posterUrl
            this.quality = quality
        }
    }

    override suspend fun search(query: String): List<SearchResponse> {
        val document = app.get("$mainUrl/?s=$query").document
        return document.select("article.item").mapNotNull {
            it.toSearchResult()
        }
    }

    override suspend fun load(url: String): LoadResponse? {
        val document = app.get(url).document

        val title = document.selectFirst("h1.entry-title")?.text() ?: return null
        val poster = document.selectFirst(".poster img")?.attr("src")
        val description = document.selectFirst(".wp-content")?.text()?.trim()

        val recommendations = document.select("article.item").mapNotNull {
            it.toSearchResult()
        }

        val type = when {
            url.contains("/series/", true) -> TvType.TvSeries
            url.contains("/drama/", true) -> TvType.AsianDrama
            else -> TvType.Movie
        }

        if (type == TvType.Movie) {
            val videoLinks = document.select("iframe").mapNotNull { iframe ->
                val src = iframe.attr("src")
                if (src.isNotBlank()) {
                    ExtractorLink(
                        name,
                        name,
                        src,
                        mainUrl,
                        Qualities.Unknown.value,
                        false
                    )
                } else null
            }

            return newMovieLoadResponse(title, url, type, videoLinks) {
                this.posterUrl = poster
                this.plot = description
                this.recommendations = recommendations
            }
        } else {
            // For TV series, extract episodes
            val episodes = document.select(".eplister ul li").mapNotNull { li ->
                val episodeTitle = li.selectFirst(".epl-title")?.text() ?: return@mapNotNull null
                val episodeNumber = li.selectFirst(".epl-num")?.text()?.filter { it.isDigit() }?.toIntOrNull() ?: 0
                val episodeData = li.selectFirst("a")?.attr("href") ?: return@mapNotNull null
                Episode(episodeData, episodeNumber, episodeTitle)
            }

            return newTvSeriesLoadResponse(title, url, type, episodes) {
                this.posterUrl = poster
                this.plot = description
                this.recommendations = recommendations
            }
        }
    }

    override suspend fun loadLinks(
        data: String,
        isCasting: Boolean,
        subtitleCallback: (SubtitleFile) -> Unit,
        callback: (ExtractorLink) -> Unit
    ): Boolean {
        val document = app.get(data).document
        document.select("iframe").mapNotNull { iframe ->
            val src = iframe.attr("src")
            if (src.isNotBlank() && src.startsWith("http")) {
                callback(
                    ExtractorLink(
                        name,
                        name,
                        src,
                        mainUrl,
                        Qualities.Unknown.value,
                        src.contains(".m3u8")
                    )
                )
            }
        }
        return true
    }
}