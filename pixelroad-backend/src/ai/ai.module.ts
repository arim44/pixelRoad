import { Module } from '@nestjs/common';
import { AiService } from './ai.service';
import { AiController } from './ai.controller';
import { AiReportService } from './ai-report.service';
import { LandmarkModule } from '../landmark/landmark.module';
import { LandmarkRecommendationService } from './landmark-recommendation.service';
import { AiClientService } from './ai-client.service';

@Module({
  imports: [LandmarkModule],
  controllers: [AiController],
  providers: [AiService, AiReportService, LandmarkRecommendationService,
            AiClientService,
  ],
})
export class AiModule {}
